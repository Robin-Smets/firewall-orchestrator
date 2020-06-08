# setup distributed servers via ansible
	- modify itsecorg/inventory/hosts to your needs 
	     change ip addresses) of hosts to install to, e.g. 
	        isofront ansible_host=10.5.5.5
	        isoback ansible_host=10.5.10.10
	- put the hosts into the correct section ([frontends], [backends], [importers])
	- make sure all target hosts meet the requirements for ansible (user with pub key auth & full sudo rights)
	- modify isohome/etc/iso.conf on frontend(s):
		enter the address of the database backend server, e.g.
			itsecorg database hostname              10.5.10.10
	- modify /etc/postgresql/x.y/main/pg_hba.conf to allow secuadmins access from web frontend(s), e.g.  
     host    all         +secuadmins         127.0.0.1/32           md5
     host    all         +secuadmins         10.5.5.5/32            md5
     host    all         dbadmin             10.5.10.10/32            md5