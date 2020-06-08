
# Instructions:

#### preparations ####
- install new machine running latest ubuntu or debian
- allow for disk space triple the size of the DB (e.g. 500 GB for a 140 GB DB) - especially needed for the vacuum step at the end
- allow for 16 GB of RAM and 4 CPUs
- install basics:
  apt install sudo net-tools ansible git ssh
- add user to sudoers (e.g. bei editing /etc/group) and reboot
- setup ssh
  ssh-keygen -b 4096
  cat .ssh/id_rsa.pub >>.ssh/authorized_keys 
- make ssh available in virtual box:
  choose NAT networking - advanced - add port forwarding for ssh from port e.g. 2222 to 22 (see http://ask.xmodulo.com/access-nat-guest-from-host-virtualbox.html)
- ssh to guest
- cp ssh pub key to isodev auth_keys
- get itsecorg from isodev git:
  git clone 192.168.100.93:/home/git/itsecorg
- install itsecorg
  cd itsecorg/ansible; ansible-playbook -i inventory site.yml -K
- allow for disk space triple the size of the DB (e.g. 500 GB for a 140 GB DB) - especially needed for the vacuum step at the end
- restore db backup:
    psql -c "create database isodb"
    psql -d isodb -f "<database-dump>"
- if you need a gui user:
   psql -d isodb -c "create user "admin" WITH PASSWORD 'fworch.1' IN GROUP isoadmins"
   or: psql -d isodb -c "alter user "admin" WITH PASSWORD 'fworch.1'"
   psql -d isodb -c "insert into isoadmin (isoadmin_id,isoadmin_first_name,isoadmin_last_name,isoadmin_username,isoadmin_password_must_be_changed) VALUES (4242,'Hans','Wurst','admin',FALSE)"

#### Optimization starts here ####
- allow for disk space triple the size of the DB (e.g. 500 GB for a 140 GB DB) - especially needed for the vacuum step at the end
- transform database tables to auto cascade thru all tables when deleting unwanted managements:
   psql -d isodb -f /usr/share/itsecorg/install/database/stored-procedures/iso-change-to-delete-cascade.sql
- start this shell script as a dbadmin to remove all unwanted managements:
  . /usr/share/itsecorg/install/database/db-maintenance/remove-2-year-old-devices.sh
- stop all db activity before the next command:
  time psql -d isodb -c "vacuum full verbose"