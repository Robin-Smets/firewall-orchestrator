import time
from datetime import datetime
from typing import List, Dict
import requests, requests.packages

from fwo_log import getFwoLogger
from fwo_config import readConfig
from fwo_const import fwo_config_filename, importer_user_name
import fwo_api
from fwo_exception import FwoApiLoginFailed
import fwo_globals

"""
    the configuraton of a firewall orchestrator itself
    as read from the global config file including FWO URI
"""
class FworchConfig():
    FwoApiUri: str
    FwoUserMgmtApiUri: str
    ApiFetchSize: int
    ImporterPassword: str
    
    def __init__(self, fwoApiUri, fwoUserMgmtApiUri, importerPwd, apiFetchSize=500):
        if fwoApiUri is not None:
            self.FwoApiUri = fwoApiUri
        else:
            self.FwoApiUFwoUserMgmtApiri = None
        if fwoUserMgmtApiUri is not None:
            self.FwoUserMgmtApiUri = fwoUserMgmtApiUri
        else:
            self.FwoUserMgmtApiUri = None
        self.ImporterPassword = importerPwd
        self.ApiFetchSize = apiFetchSize

    @classmethod
    def fromJson(cls, json_dict):
        fwoApiUri = json_dict['fwo_api_base_url']
        fwoUserMgmtApiUri = json_dict['user_management_api_base_url']
        if 'importerPassword' in json_dict:
            fwoImporterPwd = json_dict['importerPassword']
        else:
            fwoImporterPwd = None
        
        return cls(fwoApiUri, fwoUserMgmtApiUri, fwoImporterPwd)

    def __str__(self):
        return f"{self.FwoApiUri}, {self.FwoUserMgmtApi}, {self.ApiFetchSize}"

    def setImporterPwd(self, importerPassword):
        self.ImporterPassword = importerPassword        

class ManagementDetails():
    Id: int
    Name: str
    Hostname: str
    ImportDisabled: bool
    Devices: dict
    ImporterHostname: str
    DeviceTypeName: str
    DeviceTypeVersion: str
    Port: int
    ImportUser: str
    Secret: str

    def __init__(self, hostname: str, id: int, importDisabled: bool, devices: Dict, 
                 importerHostname: str, name: str, deviceTypeName: str, deviceTypeVersion: str, port: int = 443, secret: str = '', importUser: str = ''):
        self.Hostname = hostname
        self.Id = id
        self.ImportDisabled = importDisabled
        self.Devices = devices
        self.ImporterHostname = importerHostname
        self.Name = name
        self.DeviceTypeName = deviceTypeName
        self.DeviceTypeVersion = deviceTypeVersion
        self.Port = port
        self.Secret = secret
        self.ImportUser = importUser

    @classmethod
    def fromJson(cls, json_dict: Dict):
        Hostname = json_dict['hostname']
        Id = json_dict['id']
        ImportDisabled = json_dict['importDisabled']
        Devices = json_dict['devices']
        ImporterHostname = json_dict['importerHostname']
        Name = json_dict['name']
        DeviceTypeName = json_dict['deviceType']['name']
        DeviceTypeVersion = json_dict['deviceType']['version']
        Port = json_dict['port']
        ImportUser = json_dict['import_credential']['user']
        Secret = json_dict['import_credential']['secret']

        return cls(Hostname, Id, ImportDisabled, Devices, ImporterHostname, Name, DeviceTypeName, DeviceTypeVersion, port=Port, importUser=ImportUser, secret=Secret)

    def __str__(self):
        return f"{self.Hostname}({self.Id})"


"""Used for storing state during import process per management"""
class ImportState():
    ErrorCount: int
    ChangeCount: int
    ErrorString: str
    StartTime: int
    DebugLevel: int
    Config2import: dict
    ConfigChangedSinceLastImport: bool
    FwoConfig: FworchConfig
    MgmDetails: ManagementDetails
    FullMgmDetails: dict
    ImportId: int
    Jwt: str
    ImportFileName: str
    ForceImport: str
    ImportVersion: int
    DataRetentionDays: int
    DaysSinceLastFullImport: int
    LastFullImportId: int
    IsFullImport: bool

    def __init__(self, debugLevel, configChangedSinceLastImport, fwoConfig, mgmDetails, jwt, force, version=8, isFullImport=False):
        self.ErrorCount = 0
        self.ChangeCount = 0
        self.ErrorString = ''
        self.StartTime = int(time.time())
        self.DebugLevel = debugLevel
        self.Config2import = { "network_objects": [], "service_objects": [], "user_objects": [], "zone_objects": [], "rules": [] }
        self.ConfigChangedSinceLastImport = configChangedSinceLastImport
        self.FwoConfig = fwoConfig
        self.MgmDetails = ManagementDetails.fromJson(mgmDetails)
        self.FullMgmDetails = mgmDetails
        self.ImportId = None
        self.Jwt = jwt
        self.ImportFileName = None
        self.ForceImport = force
        self.ImportVersion = int(version)
        self.IsFullImport = isFullImport

    def __str__(self):
        return f"{str(self.ManagementDetails)}({self.age})"
    
    def setImportFileName(self, importFileName):
        self.ImportFileName = importFileName

    def setImportId(self, importId):
        self.ImportId = importId

    def setChangeCounter(self, changeNo):
        self.ChangeCount = changeNo

    def setErrorCounter(self, errorNo):
        self.ErrorCount = errorNo

    def setErrorString(self, errorStr):
        self.ErrorString = errorStr

    @classmethod
    def initializeImport(cls, mgmId, debugLevel=0, suppressCertWarnings=False, sslVerification=False, force=False, version=8):

        def check_input_parameters(mgmId):
            if mgmId is None:
                raise BaseException("parameter mgm_id is mandatory")

        logger = getFwoLogger()
        check_input_parameters(mgmId)

        fwoConfig = FworchConfig.fromJson(readConfig(fwo_config_filename))

        # authenticate to get JWT
        try:
            jwt = fwo_api.login(importer_user_name, fwoConfig.ImporterPassword, fwoConfig.FwoUserMgmtApiUri)
        except FwoApiLoginFailed as e:
            logger.error(e.message)
            return e.message
        except:
            return "unspecified error during FWO API login"

        # set global https connection values
        fwo_globals.setGlobalValues (suppress_cert_warnings_in=suppressCertWarnings, verify_certs_in=sslVerification, debug_level_in=debugLevel)
        if fwo_globals.verify_certs is None:    # not defined via parameter
            fwo_globals.verify_certs = fwo_api.get_config_value(fwoConfig.FwoApiUri, jwt, key='importCheckCertificates')=='True'
        if fwo_globals.suppress_cert_warnings is None:    # not defined via parameter
            fwo_globals.suppress_cert_warnings = fwo_api.get_config_value(fwoConfig.FwoApiUri, jwt, key='importSuppressCertificateWarnings')=='True'
        if fwo_globals.suppress_cert_warnings: # not defined via parameter
            requests.packages.urllib3.disable_warnings()  # suppress ssl warnings only    

        try: # get mgm_details (fw-type, port, ip, user credentials):
            mgmDetails = fwo_api.get_mgm_details(fwoConfig.FwoApiUri, jwt, {"mgmId": int(mgmId)}, debugLevel) 
        except:
            logger.error("import_management - error while getting fw management details for mgm=" + str(mgmId) )
            raise

        # return ImportState (int(debugLevel), True, fwoConfig, mgmDetails) 
        return ImportState (
            debugLevel = int(debugLevel),
            configChangedSinceLastImport = True,
            fwoConfig = fwoConfig,
            mgmDetails = mgmDetails,
            jwt = jwt,
            force = force,
            version = version
        )

    def setPastImportInfos(self):
        
        logger = getFwoLogger()
        
        try: # get past import details (LastFullImport, ...):
            self.DataRetentionDays, self.LastFullImportId, lastFullImportDate = \
                fwo_api.getLastImportDetails(self.FwoConfig.FwoApiUri, self.Jwt, {"mgmId": int(self.MgmDetails.Id)}, self.DebugLevel) 
        except:
            logger.error(f"import_management - error while getting past import details for mgm={str(self.MgmDetails.Id)}")
            raise

        if lastFullImportDate is not None:
            # Convert the string to a datetime object
            pastDate = datetime.strptime(lastFullImportDate, "%Y-%m-%dT%H:%M:%S.%f")
            now = datetime.now()
            difference = now - pastDate
            self.DaysSinceLastFullImport = difference.days
        else:
            self.DaysSinceLastFullImport = 0
