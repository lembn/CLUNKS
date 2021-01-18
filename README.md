# CLUNKS

***C**ommand* ***L**ine* ***U**nification* ***N***etwor***k*** ***S**ystem*

**CLUNKS** is a system to provide simple LAN video conferencing for large businesses and establishments. Users only need to create a **CLUNK** server on their network, and they will be able to host and join video calls with anyone on that network.

---

## Servers
When the user first installs the product, they will need to create a *CLUNK server* to be able to do anything. The CLUNK server is the server-side program which manages all sub-servers and serves clients.

To create a CLUNK server, the user must be an admin/superuser on their system. They will run a setup program on their intended server machine that will allow the admin to conigure IPs, create sub-servers, manage user elevation levels, etc. After the the server has been configured it only needs to be started.

A CLUNK server hosts *sub-servers*. Sub-servers are designed to create physical separation withing the server, each sub-server will have its own database. Because of this design, a user registered to a sub-server, will not exist in any other sub-server unless they are created in the other sub-servers also. This allows the admin of the CLUNK server to create separation within the server. For example, a school may use CLUNKS for meetings, but create seperate subservers for each year group.

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

*Notifications:* ```notifications``` will show the any notifications the user has from the server, such as missed calls. The user can clear their notifications, otherwise all uncleared notifications will be loaded when the command is run. (Add limit per user to prevent inifite database expansion?)

*Stats:* ```stats``` will show logged statistics to the user. This can include information such as processed packets per second; how many datagrams are too large for the buffer size as a percentage, etc. The user can use these statistics to make informed decisions on which settings to set in ```settings```

*Settings:* ```settings``` will allow the user to configure the program to run differently to optimise efficiency and improve the user experience for them personally.

----

## Technical Specifications
 - Written in C# (.NET Core)
 - Email password recovery
 - Heartbeat standards follow IEEE spec: https://stackoverflow.com/questions/1442189/heartbeat-protocols-algorithms-or-best-practices

### Data Flow
The program will use sockets to send data over the network the UDP transmission protocol. A broadcasting user will send: the frame of their video, the audio frame, which user they are and the total size of the data in a C# class object seriliazed into XML. The receiving user will display the frames using the Gstreamer multimedia library. The user identification will only be used when managing calls with more than 2 members, but will be present in all data objects as part of the protocol used by CLUNKS.

UDP is being used because it creates very small packets, (about 60% smaller than TCP). It is also much faster than TCP by nature because it doesn't contain the slow error checking methods that TCP uses, doesn't wait for acknowledgement from the receiver, is connectionless, so an active connection doesn't need to be managed, doesn't compensate for lost packets and also doesn't attempt to guarantee packet delivery. Although this means that the packets recieved by the user may not be an accurate representation of what was originally sent, the eventual consistency reliant nature of the protocol (the philosophy that even if a few audiovisual frames are dropped in the process, the overall data received should be good enough to provide a good user experience) combined with the speed of data transfer makes it ideal for audiovisual streaming over a network.

### Security (Server)
The CLUNK server will use SQLite for database managmemnt. The only senstive information stored in the databases on the CLUNK server are the passwords used for user, rooms and groups. They will all be hashed with bcrypt.

### Security (User)
The combination of encryption and hashing will be used to create a digital certificate protocol that provides security, integrity and confidentiality.

First the data will be encrypted using the receiver's public key. The ciphertext produced by the encryption will then be hashed to create a digest. After hashing, the digest will be encrypted with the sender's private key (not public). This produces the signature. The signature will be sent alognside the original ciphertext.

When the receiver receives the data, they will first decrypt the signature using the sender's public key (not private). The receiver will then perform a hash function on the ciphertext. The hash function must be used by both parties. If the digest generated by the receiver is the same hash provided by the sender, then the transfer can be authenticated.

This process provides: security via the encryption; integrity via the hash (because if the data was changed the hashes wouldn't match) and confidentially via the signing of the asymmetric keys (since the intended sender would be the only person who had the correct private key to be able to sign the original digest).

This process will use RSA for the asymmetric encryption and a relatively cheap hash function for digesting. The hash function has to be cheap because if the hashes take to long to compute, the process will create heavy latency in the system. The user will be assigned RSA keys upon joining a sub-server.

# Research
C# Send Email: https://www.google.com/search?rlz=1C1CHBF_en-GBGB777GB777&sxsrf=ALeKk031_qPKoOIFowLL7Lrg2_e-ZTZgCw%3A1610481594743&ei=uv_9X-TcLPOF1fAP3Pu5wAc&q=c%23+send+email+smtp&oq=c%23+send+emai&gs_lcp=CgZwc3ktYWIQAxgBMgQIIxAnMgcIABDJAxBDMgUIABCRAjIECAAQQzIECAAQQzICCAAyAggAMgIIADICCAAyAggAOgQIABBHOgcIIxDJAxAnOgUIABCxAzoKCAAQsQMQFBCHAjoHCAAQFBCHAjoICAAQsQMQgwE6BAgAEApQn0FY7UlglVRoAHACeACAAeYBiAHbCJIBBTUuNC4xmAEAoAEBqgEHZ3dzLXdpesgBCMABAQ&sclient=psy-ab

C# Access Webcam: https://www.google.com/search?q=c%23+access+webcam&rlz=1C1CHBF_en-GBGB777GB777&oq=c%23+acc&aqs=chrome.0.69i59j69i57j69i58j69i60l2.1166j0j7&sourceid=chrome&ie=UTF-8

C# Socket Programming: https://www.youtube.com/channel/UCUg_M6wvaS-DhHpFpW7aC6w

C# Customise Command Prompt: https://www.dotnetperls.com/console-color