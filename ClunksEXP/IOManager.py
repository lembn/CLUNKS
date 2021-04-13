import os
import pickle
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
        failed = False

        def LoadUsers(users):
            global failed
            for user in users:
                username = user.get('username', -1)
                if username == -1:
                    logFunc(f'LOAD FAILED: User has no name attribute.')
                    failed = True
                userPwd = user.get('password', -1)
                if userPwd == -1:
                    logFunc(f'LOAD FAILED: User has no password attribute.')
                    failed = True
                userSectors = user.get('sectors', [])
                if not isinstance(userSectors, list):
                    userSectors = [userSectors]
                userList = [[username, userPwd, userSectors, user.get('global')]]
                self.storage['user'].write(pickle.dumps(userList))
                failed = False

        def LoadRooms(rooms, parent):
            global failed
            for room in rooms:
                roomName = room.get('name', -1)
                if roomName == -1:
                    logFunc(f'LOAD FAILED: Room has no name attribute.')
                    failed = True
                roomPwd = room.get('password', -1)
                if roomName == -1:
                    logFunc(f'LOAD FAILED: Room has no password attribute.')
                    failed = True
                roomSectors = room.get('sectors', [])
                if not isinstance(roomSectors, list):
                    roomSectors = [roomSectors]
                roomList = [[roomName, roomPwd, parent, roomSectors, room.get('global')]]
                self.storage['room'].write(pickle.dumps(roomList))
                LoadUsers(room.findall('user'))
                LoadRooms(room.findall('room'), roomName)
                failed = False

        self.Cleanup()
        self.storage = {'user': tempfile.TemporaryFile(), 'subserver': tempfile.TemporaryFile(), 'room': tempfile.TemporaryFile(), 'elevation': tempfile.TemporaryFile()}
        data = expFile.read()
        if not data:
            logFunc(f"LOAD FAILED: '{expFile.name}' is empty.")
            failed = True
        root = ET.fromstring(data)
        subservers = root.findall('subservers')[0]
        if not subservers:
            logFunc('LOAD FAILED: The selected EXP has no subservers.')
            failed = True
        for subserver in subservers:
            subserverName = subserver.get('name', -1)
            if subserverName == -1:
                logFunc(f'LOAD FAILED: Subserver {subservers.index(subserver)} has no name attribute.')
                failed = True
            subserverSectors = subserver.get('sectors', [])
            if not isinstance(subserverSectors, list):
                subserverSectors = [subserverSectors]
            subserverList = [[subserverName, subserverSectors]]
            self.storage['subserver'].write(pickle.dumps(subserverList))
            LoadRooms(subserver.findall('room'), subserverName)
            LoadUsers(subserver.findall('user'))

        #Load elevations
        elevations = root.findall('elevations')[0]
        for elevation in elevations:
            elevationName = elevation.get('name', -1)
            if elevationName == -1:
                logFunc(f'LOAD FAILED: Elevation {elevations.index(elevation)} has no name attribute.')
                failed = True
            privilege = elevation.get('privilege', -1)
            if privilege == -1:
                logFunc(f'LOAD FAILED: Elevation {elevations.index(elevation)} has no privilege attribute.')
                failed = True
            privileges = ['True' if i == '1' else 'False' for i in bin(int(privilege))[2:]]
            while len(privileges) < NUM_PRIV:
                privileges = ['False'] + privileges
            elevationSectors = elevation.get('sectors', [])
            if not isinstance(elevationSectors, list):
                elevationSectors = [elevationSectors]
            elevationList = [[elevationName] + privileges + elevationSectors]
            self.storage['elevation'].write(pickle.dumps(elevationList))

        if not failed:
            logFunc(f"Sucessfully loaded '{expFile.name}'")

    def Export(self, logFunc, expFile):
        noSectorSubservers = {}
        noSectorRooms = {}
        sectorToSubserver = {}
        sectorToRoom = {}

        def ApplySectorsRecrusive(index, sectors, searchSubservers = False):
            if searchSubservers:
                for sector in sectors:
                    if sector:
                        try:
                            sectorToSubserver[sector.strip()].append(index)
                        except KeyError:
                            sectorToSubserver[sector.strip()] = [index]
                del noSectorSubservers[index]
            else:
                for sector in sectors:
                    if sector:
                        try:
                            sectorToRoom[sector.strip()].append(index)
                        except KeyError:
                            sectorToRoom[sector.strip()] = [index]
                del noSectorRooms[index]
                found = False
                for parent, child in noSectorRooms.items():
                    if child == index:
                        ApplySectorsRecrusive(parent, sectors)
                        found = True
                        break
                if not found:
                    for parent, child in noSectorSubservers.items():
                        if child == index:
                            ApplySectorsRecrusive(parent, sectors, True)
                            break

        usernames = []
        subserverNames = []
        roomNames = []
        elevationNames = []
        for user in self.ReadAll(self.storage['user']):
            name = user[0].lower().strip()
            if name not in usernames:
                usernames.append(name)
            else:
                logFunc('EXPORT FAILED: Usernames must be unique.')
                return
        for subserver in self.ReadAll(self.storage['subserver']):
            name = subserver[0].lower().strip()
            if name not in subserverNames:
                subserverNames.append(name)
            else:
                logFunc('EXPORT FAILED: Subserver names must be unique.')
                return
        for room in self.ReadAll(self.storage['room']):
            name = room[0].lower().strip()
            if name not in roomNames and name not in subserverNames:
                roomNames.append(name)
            else:
                logFunc('EXPORT FAILED: Room/subserver names must be unique.')
                return
        for elevation in self.ReadAll(self.storage['elevation']):
            name = elevation[0].lower().strip()
            if name not in elevationNames:
                elevationNames.append(name)
            else:
                logFunc('EXPORT FAILED: Elevation names must be unique.')
                return

        nameToSubserver = {}
        subserverElements = []
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
            if not entities[x][1].split(',')[0]:
                noSectorSubservers[x] = None
            else:
                for sector in entities[x][1].split(','):
                    if sector:
                        try:
                            sectorToSubserver[sector.strip()].append(x)
                        except KeyError:
                            sectorToSubserver[sector.strip()] = [x]

        nameToRoom = {}
        roomElements = []
        processedRooms = []
        entities = self.ReadAll(self.storage['room'])
        #Rooms with subserver parents
        for x in range(len(entities)):
            if entities[x][2] in subserverNames:
                room = entities[x]
                processedRooms.append(x)
                roomElements.append(ET.SubElement(subserverElements[nameToSubserver[room[2]]], 'room', {'name': room[0], 'password': room[1]}))
                nameToRoom[room[0]] = len(roomElements) - 1
                parentIndex = nameToSubserver[room[2]]
                if not room[3].split(',')[0]:
                    if parentIndex in noSectorSubservers.keys():
                        noSectorSubservers[parentIndex] = len(roomElements) - 1
                    noSectorRooms[len(roomElements) - 1] = None
                else:
                    for sector in room[3].split(','):
                        if sector:
                            if parentIndex in noSectorSubservers.keys():
                                try:
                                    sectorToSubserver[sector.strip()].append(len(subserverElements) - 1)
                                except KeyError:
                                    sectorToSubserver[sector.strip()] = [len(subserverElements) - 1]
                                del noSectorSubservers[parentIndex]
                            try:
                                sectorToRoom[sector.strip()].append(len(roomElements) - 1)
                            except KeyError:
                                sectorToRoom[sector.strip()] = [len(roomElements) - 1]
            elif entities[x][2] not in roomNames:
                logFunc(f"EXPORT FAILED: Parent of room '{entities[x][0]}' does not exist.")
                return

        #Rooms with room parents
        for x in range(len(entities)):
            if x not in processedRooms:
                room = entities[x]
                parent = roomElements[nameToRoom[entities[x][2]]]
                roomElements.append(ET.SubElement(parent, 'room', {'name': entities[x][0], 'password': entities[x][1]}))
                nameToRoom[room[0]] = len(roomElements) - 1
                parentIndex = nameToRoom[room[2]]
                if not room[3].split(',')[0]:
                    if parentIndex in noSectorRooms.keys():
                        noSectorRooms[parentIndex] = len(roomElements) - 1
                    noSectorRooms[len(roomElements) - 1] = None
                else:
                    if parentIndex in noSectorRooms.keys():
                        if isinstance(room[3], list):
                            ApplySectorsRecrusive(parentIndex, room[3])
                        else:
                            ApplySectorsRecrusive(parentIndex, [room[3]])
                    for sector in entities[x][3].split(','):
                        if sector:
                            try:
                                sectorToRoom[sector.strip()].append(len(roomElements) - 1)
                            except KeyError:
                                sectorToRoom[sector.strip()] = [len(roomElements) - 1]

        elevationElements = []
        sectorToElevation = {}
        elevationRoot = None
        globalUserRoot = ET.SubElement(root, 'globalUsers')
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
            elevationName = None
            for sector in user[2].split(','):
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
            if user[3] == 'True':
                ET.SubElement(globalUserRoot, 'user', {'username': user[0], 'password': user[1] , 'sectors': user[2], 'global': user[3], 'elevation': elevationName})
                continue
            sectorToParent = {}
            for sector in user[2].split(','):
                sector = sector.strip()
                if sector in sectorToSubserver.keys():
                    try:
                        sectorToParent[sector][0] += [subserverElements[index] for index in sectorToSubserver[sector]]
                    except KeyError:
                        sectorToParent[sector] = [[subserverElements[index] for index in sectorToSubserver[sector]], []]
                else:
                    try:
                        sectorToParent[sector][1] += [roomElements[index] for index in sectorToRoom[sector]]
                    except KeyError:
                        sectorToParent[sector] = [[roomElements[index] for index in sectorToRoom[sector]], []]
            for sector, parents in sectorToParent.items():
                finalParents = []
                for parent in parents[0] + parents[1]:
                    parentSectors = parent.get('sectors')
                    if not parentSectors:
                        parentSectors = sector
                    else:
                        parentSectors += f',{sector}'
                    parent.set('sectors', parentSectors)
                    lowest = True
                    for child in parent.iter():
                        if child is not parent and child in parents[1]:
                            lowest = False
                    if lowest:
                        finalParents.append(parent)
                for parent in finalParents:
                    ET.SubElement(parent, 'user', {'username': user[0], 'password': user[1] , 'sectors': user[2], 'global': user[3], 'elevation': elevationName})

        expFile.write(ET.tostring(root).decode())
        logFunc(f'Exported to: {expFile.name}')