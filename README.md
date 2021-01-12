# CLUNKS

***C**ommand* ***L**ine* ***U**nification* ***N***etwor***k*** ***S**ystem*

**CLUNKS** is a system to provide simple LAN video conferencing for large businesses and establishments. Users only need to create a **CLUNK** server on their network, and they will be able to host and join video calls with anyone on that network.

## User Flow
### New Server
The user first needs to create the **CLUNK** server, this will manage all the user accounts, security and direct traffic. The user will specify the IP:port that the server should host to and the server will configure itself with these settings and set its admin to the user who created the server.
```
>>> server [servername] [ip]:[port]
```

### New User
To use **CLUNKS**, the user will need to register an account on the server. Accounts are used to track the contacts of a user, manage their server privelleges and track which rooms the user is in. The user will register a username and a password, other users will refer to them by that username, and the password will be used for validation. The server will hash the password, then save the username and password in an ecnrypted database.
```
>>> user [username] [password]
```

### Existing User
After a user has been registered to the server, they will need to add contacts:
```
>>> request [username]
```
This will send a request to the desired user, which can be accepted or rejected by them. Requests are stored in the database by the server until they are accepted or rejected. To avoid this process, the user can run:
```
>>> request [username] [password]
```
where password is the password of the user they're trying to add. This will automatically add the contacts for both users without creating a request.
To call this contact, the user would run:
```
>>> call [username]
```
To call multiple people simultaneously, the user would need to create a room. Rooms are logical groups of users who can all call each other at the same time. Members of a room have the choice to the ongoing call of a room.

The command to create a room is:
```
>>> room [roomname] [password]
```
*Where [password] is optional*

The above command will create a room if it doesn't exist, or join the existing one it does. To then call this room, the user would run:
```
>>> call [roomname]
```
This would create a call for the room if one doesn't exist, or join the existing one if it does.

When calling a room, you can only see the webcam of people in your contacts. To see everyone the user would either need to request everyone in the room:
```
>>> request [username] [password]
```
*Where [password] is optional, but if it is present and correct, then the contacts are automatically added without creaeting requests*

Or they could enter the room:
```
>>> enter [roomname] [password]
[roomname] >>> ...
```
*Where [password] is optional*

When a user enters a room, they can call the room with:
```
>>> call
```
Or call a specific person within that room:
```
>>> call [username]
```
All while having nobody in that room in their server contacts.

The user can leave the room with:
```
>>> leave
```
Which will undo anything done by ```enter``` or can exit the room with ```exit``` which would remove the user from the room's contact list.

There can be rooms within rooms<br>
There should be commands for the user to see their contacts, joined rooms and open (incoming and outgoing) requests<br>
There should be commands to show room structure, rooms within rooms and user elevation levels<br>

## Things
Asymmetric symmetric encryption <br>
Handshake (and digest) <br>
Different user levels have different privelleges <br>
Elevation codes <br>
Groups (temporary rooms) <br>

## Maybes
 - email password recovery??