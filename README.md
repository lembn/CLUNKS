# CLUNKS

***C**ommand* ***L**ine* ***U**nification* ***N***etwor***k*** ***S**ystem*

**CLUNKS** is a system to provide simple LAN video conferencing for large businesses and establishments. Users only need to create a **CLUNK** server on their network, and they will be able to host and join video calls with anyone on that network.

---

## Servers
When the user first installs the product, they will need to create a *CLUNK server* to be able to do anything. The CLUNK server is the server-side program which manages all sub-servers and serves clients.

To create a CLUNK server, the user must be an admin/superuser on their system. They will run a setup program on their intended server machine that will allow the admin to conigure IPs, create sub-servers, manage user elevation levels, etc. After the the server has been configured it only needs to be started.

A CLUNK server hosts *sub-servers*. Sub-servers are designed to create physical separation withing the server. Each sub-server will have its own database, which will be encrypted. Because of this design, a user registered to a sub-server, will not exist in any other sub-server unless they are created in the other sub-servers also. This allows the admin of the CLUNK server to create separation within the server. For example, a school may use CLUNKS for meetings, but create seperate subservers for each year group.

The server admin can also create *global users*. These are users who are not tied to one specific sub-sever but exist globally to the entire server. In the school scenario, this would be used to create accounts for teachers, since it allows the admin to create the user once, rather than making a new one for each year group sub-sever.

The ability to run commands within CLUNKS is controlled by the server admin. The server-admin can grant different permissions to different users which will allow them to do certain things. In the school example, the server admin may configure the server such that only teachers can start calls, and sudents may only join existing ones. To achive this, the would create an *elevation level* (one for the student and one for the teacher). Elevation levels describe the actions that a user is allowed to make. The teacher elevation level would have the ability to make calls, the student one would not. These elevation levels can then be assigned to the users as the admin sees fit.

---

## Using **CLUNKS**
As the name suggests, **CLUNKS**, is a command line application, the recommded usage is to add **CLUNKS** to the user's environment variables so they can call the program from their command prompt/terminal.

To run the program, users will call ```clunks```, (or whatever the program is named in the user's environment variables). They can tell they're in the **CLUNKS** environment as their command promt/terminal will change to:
```
CLNKS>>>
```
***CLUNKS** commands are not case sensitive*

### **Logging in**
A user can enter a sub-server with:
```
CLNKS>>> connect [subservername] [username]
```
If a user with the provided username exists on on the sub-server, the sub-server will reply with:
```
[subservername]
CLNKS>>> Enter your password:
```
The exit command is CTRL+E.

After the initial login, one of the first things a user might want to do is change their password from whatever was assinged to them by the admin, they can do this with:
```
[subservername]
CLNKS>>> changepwd [old password] [new password]
```

### **Calling**
After a user has entered the sub-server, they can create calls. To call another user, they can run:
```
[subservername]
CLNKS>>> call [username]
```
This will send a call to the desired user, which can be accepted or rejected by them. Call requests look like:
```
[subservername]
CLNKS>>> [username] is calling. Accept?
```
The user can enter ```y``` to accept or ```n``` to decline. After two seconds with no response, the request is repeated again. Requests are repeated 10 times until they are automatically quit. The orignal user would be informed that the call was not picked up.

For large conference calls, the user can also create a call on the server with the ```call``` command This creates a conference call that anyone on the subserver can join into by running ```joincall```.

### **Rooms**
For group calls, the user can call the sub-server, and any member of the sub-server is able to join that call, however if a user wanted to make a group call that didn't include all members of the sub-server, they can create a room.

Rooms are logical partitions of a sub-server. Unlike sub-servers, they do not have their own database (hence logical), but are entities within the sub-server's database. This allows for partial separation within sub-servers for organisational purposes.

Unlike sub-servers, rooms can be created from the client-side. This can be done with:
```
[subservername]
CLNKS>>> makeroom [roomname] [password]
```
*Where [password] is optional*

To join a room, the user would run:
```
[subservername]
CLNKS>>> joinroom [roomname] [password]
```
Rooms have the same behaviour as sub-servers, but rooms can be infinitely created within each other. When in a room, a user can only call other members of the room. The user can exit the room with CTRL+E.

### **Groups**
Groups are similar to rooms, except they are temporary. They serve the purpose of allowing the user to make privatised group calls without needing to impact the structure of the sub-server.

A user can create a group with ```makegroup``` or join a group with ```joingroup```.

The group will exist until all its members quit the program. If they leave the group but still have the program open, the group will be kept alive until the last group member quits the program (in case anyone wants to rejoin).

### **User commands**
There are commands that users can run to obtain information about the subserver. 

*Contacts:* ```contacts``` will show all the contacts of whatever space the user is in. If the user runs ```contacts``` from a sub-server, they will see a list of all the sub-server's members, but running ```contacts``` from a room will show the contact list of the current room.

*Info:* ```info``` will show the information of the user who calls it, it can show things like: granted permissions, joined rooms, number of calls, etc.

*Structure:* ```stucture``` will show the structure of the current sub-server, in a tree-type view. This includes any rooms and groups that haven't been marked as hidden.

## Things
Asymmetric symmetric encryption <br>
Handshake (and digest) <br>
Email password recovery??