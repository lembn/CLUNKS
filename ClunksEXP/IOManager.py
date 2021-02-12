import os
import pickle
import bcrypt
import tempfile
from tkinter import messagebox
import xml.etree.ElementTree as ET

from ThreadingHelper import QUIT

NUM_PRIV = 10

class IOManager:
    def __init__(self):
        self.storage = {'user': tempfile.TemporaryFile(), 'subserver': tempfile.TemporaryFile(), 'room': tempfile.TemporaryFile(), 'elevation': tempfile.TemporaryFile()}

    def Cleanup(self):
        for f in self.storage.values():
            f.close()

    def Save(self, fileObj, values):
        fileObj.seek(0)
        fileObj.write(pickle.dumps(values))

    def ReadAll(self, fileObj):
        fileObj.seek(0)
        data = fileObj.read()
        if data:
            return pickle.loads(data)
        else:
            return []

    def LoadTemp(self, fileObj):
        try:
            return self.ReadAll(fileObj)
        except:
            messagebox.showerror('Oh no!', 'Data has been corrupted, please restart the program.')
            QUIT.set()

    def LoadExp(self, logFunc, expFile):
        def LoadUsers(users):
            for user in users:
                username = user.get('username', None)
                if not username:
                    logFunc(f'LOAD FAILED: User has no name attribute.')
                    return
                userPwd = user.get('password', None)
                if not userPwd:
                    logFunc(f'LOAD FAILED: User has no password attribute.')
                    return
                userSectors = user.get('sectors')
                userList = [[username, userPwd, userSectors]]
                self.storage['user'].write(pickle.dumps(userList))
                return

        def LoadRooms(rooms, parent):
            for room in rooms:
                roomName = room.get('name', None)
                if not roomName:
                    logFunc(f'LOAD FAILED: Room has no name attribute.')
                    return
                roomPwd = room.get('password', None)
                if not roomName:
                    logFunc(f'LOAD FAILED: Room has no password attribute.')
                    return
                roomSectors = room.get('sectors')
                roomList = [[roomName, roomPwd, parent, roomSectors]]
                self.storage['room'].write(pickle.dumps(roomList))
                LoadUsers(room.findall('user'))
                LoadRooms(room.findall('room'), roomName)
                return

        self.Cleanup()
        self.storage = {'user': tempfile.TemporaryFile(), 'subserver': tempfile.TemporaryFile(), 'room': tempfile.TemporaryFile(), 'elevation': tempfile.TemporaryFile()}
        data = expFile.read()
        if not data:
            logFunc(f"LOAD FAILED: '{expFile.name}' is empty.")
            return
        root = ET.fromstring(data)
        subservers = root.findall('subservers')[0]
        if not subservers:
            logFunc('LOAD FAILED: The selected EXP has no subservers.')
            return
        for subserver in subservers:
            subserverName = subserver.get('name', None)
            if not subserverName:
                logFunc(f'LOAD FAILED: Subserver {subservers.index(subserver)} has no name attribute.')
                return
            subserverSectors = subserver.get('sectors')
            subserverList = [[subserverName, subserverSectors]]
            self.storage['subserver'].write(pickle.dumps(subserverList))
            LoadRooms(subserver.findall('room'), subserverName)
            LoadUsers(subserver.findall('user'))

        #Load elevations
        elevations = root.findall('elevations')[0]
        for elevation in elevations:
            elevationName = elevation.get('name', None)
            if not elevationName:
                logFunc(f'LOAD FAILED: Elevation {elevations.index(elevation)} has no name attribute.')
                return
            privilege = elevation.get('privilege', None)
            if privilege == None:
                logFunc(f'LOAD FAILED: Elevation {elevations.index(elevation)} has no privilege attribute.')
                return
            privileges = ['True' if i == '1' else 'False' for i in bin(int(privilege))[2:]]
            while len(privileges) < NUM_PRIV:
                privileges = ['False'] + privileges
            elevationSectors = elevation.get('sectors', None)
            elevationList = [[elevationName] + privileges + [elevationSectors]]
            self.storage['elevation'].write(pickle.dumps(elevationList))

        logFunc(f"Sucessfully loaded '{expFile.name}'")


    def Export(self, logFunc, expFile):
        roomNames = []
        subserverNames = []
        elevationNames = []
        for subserver in self.ReadAll(self.storage['subserver']):
            if subserver[0] not in subserverNames:
                subserverNames.append(subserver[0])
            else:
                logFunc('EXPORT FAILED: Subserver names must be unique.')
                return
        for room in self.ReadAll(self.storage['room']):
            if room[0] not in roomNames and room[0] not in subserverNames:
                roomNames.append(room[0])
            else:
                logFunc('EXPORT FAILED: Room/subserver names must be unique.')
                return
        for elevation in self.ReadAll(self.storage['elevation']):
            if elevation[0] not in elevationNames:
                elevationNames.append(elevation[0])
            else:
                logFunc('EXPORT FAILED: Elevation names must be unique.')
                return

        subserverElements = []
        sectorToSubserver = {}
        nameToSubserver = {}
        entities = self.ReadAll(self.storage['subserver'])
        subserverRoot = None
        root = ET.Element('root')
        if entities:
            subserverRoot = ET.SubElement(root, 'subservers')
        else:
            logFunc('EXPORT FAILED: There are no subservers.')
            return

        for x in range(len(entities)):
            subserverElements.append(ET.SubElement(subserverRoot, 'subserver', {'name': entities[x][0]}))
            nameToSubserver[entities[x][0]] = x
            for sector in entities[x][1].split(','):
                if sector:
                    sectorToSubserver[sector] = x

        roomElements = []
        sectorToRoom = {}
        nameToRoom = {}
        processedRooms = []
        entities = self.ReadAll(self.storage['room'])
        #Rooms with subserver parents
        for x in range(len(entities)):
            if entities[x][2] in subserverNames:
                room = entities[x]
                processedRooms.append(x)
                roomElements.append(ET.SubElement(subserverElements[nameToSubserver[room[2]]], 'room', {'name': room[0], 'password': room[1]}))
                nameToRoom[room[0]] = x
                for sector in room[3].split(','):
                    try:
                        sectorToRoom[sector].append(x)
                    except KeyError:
                        sectorToRoom[sector] = [x]
            elif entities[x][2] not in roomNames:
                logFunc(f"EXPORT FAILED: Parent of room '{entities[x][0]}' does not exist.")
                return

        #Rooms with room parents
        for x in range(len(entities)):
            if x not in processedRooms:
                parent = None
                parentName = entities[x][2]
                if parentName in nameToSubserver.keys():
                    parent = subserverElements[nameToSubserver[parentName]]
                elif parentName in nameToRoom.keys():
                    parent = roomElements[nameToRoom[parentName]]
                roomElements.append(ET.SubElement(parent, 'room', {'name': entities[x][0], 'password': entities[x][1]}))
                for sector in entities[x][3].split(','):
                    try:
                        sectorToRoom[sector].append(x)
                    except KeyError:
                        sectorToRoom[sector] = [x]

        elevationElements = []
        sectorToElevation = {}
        elevationRoot = None
        entities = self.ReadAll(self.storage['elevation'])
        if entities:
            elevationRoot = ET.SubElement(root, 'elevations')
        for x in range(len(entities)):
            privilege = sum([2**i if j == 'True' else 0 for i, j in enumerate(reversed(entities[x][1:len(entities[x]) - 1]))])
            attrs = {'name': entities[x][0], 'privilege': str(privilege), 'sectors': entities[x][len(entities[x]) - 1]}
            elevationElements.append(ET.SubElement(elevationRoot, 'elevation', attrs))
            for sector in entities[x][len(entities[x]) - 1].split(','):
                sectorToElevation[sector] = x

        entities = self.ReadAll(self.storage['user'])
        for user in entities:
            parents = {}
            elevationName = None
            for sector in user[2].split(','):
                if sector in sectorToSubserver.keys():
                    parentIndexes = sectorToSubserver[sector]
                    try:
                        for index in parentIndexes:
                            parents[sector].append(subserverElements[index])
                    except KeyError:
                        parents = {sector: [subserverElements[index] for index in parentIndexes]}
                if sector in sectorToRoom.keys():
                    parentIndexes = sectorToRoom[sector]
                    try:
                        for index in parentIndexes:
                            parents[sector].append(roomElements[index])
                    except KeyError:
                        parents = {sector: [roomElements[index] for index in parentIndexes]}
                if sector in sectorToElevation.keys():
                    if not elevationName:
                        userElevation = elevationElements[sectorToElevation[sector]]
                        elevationName = userElevation.get('name')
                    else:
                        logFunc(f"EXPORT FAILED: Elevation conflict on user '{user[0]}'.")
                        return
            if elevationName == None:
                logFunc(f"EXPORT FAILED: No elevation apllied to user '{user[0]}'.")
                return
            for sector, parentList in parents.items():
                for parent in parentList:
                    parentSectors = parent.get('sectors')
                    if not parentSectors:
                        parentSectors = sector
                    else:
                        parentSectors += f',{sector}'
                    parent.set('sectors', parentSectors)
                    hashpwd = bcrypt.hashpw(user[1].encode('utf-8'), bcrypt.gensalt())
                    ET.SubElement(parent, 'user', {'username': user[0], 'password': hashpwd , 'sectors': user[2], 'elevation': elevationName})

        expFile.write(ET.tostring(root).decode())
        logFunc(f'Exported to: {expFile.name}')