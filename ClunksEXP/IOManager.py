import os
import pickle
import tempfile
from tkinter import messagebox
import xml.etree.ElementTree as ET

from ThreadingHelper import QUIT

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

    def LoadExp(self):
        pass

    def Export(self, logFunc, expFile):
        roomNames = []
        subserverNames = []
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
        entities = self.ReadAll(self.storage['room'])
        for x in range(len(entities)):
            if entities[x][2] in subserverNames:
                room = entities[x]
                entities.remove(room)
                roomElements.append(ET.SubElement(subserverElements[nameToSubserver[room[2]]], 'room', {'name': room[0], 'password': room[1]}))
                nameToRoom[room[0]] = x
                for sector in room[3].split(','):
                    sectorToRoom[sector] = x
            elif entities[x][2] not in roomNames:
                logFunc(f"EXPORT FAILED: Parent of room '{entities[x][0]}' does not exist.")
                return

        for x in range(len(entities)):
            parent = None
            parentName = entities[x][2]
            if parentName in nameToSubserver.keys():
                parent = subserverElements[nameToSubserver[parentName]]
            elif parentName in nameToRoom.keys():
                parent = roomElements[nameToRoom[parentName]]
            roomElements.append(ET.SubElement(parent, 'room', {'name': entities[x][0], 'password': entities[x][1]}))
            for sector in entities[x][3].split(','):
                sectorToRoom[sector] = x

        elevationElements = []
        sectorToElevation = {}
        elevationRoot = None
        entities = self.ReadAll(self.storage['elevation'])
        if entities:
            elevationRoot = ET.SubElement(root, 'elevations')
        for x in range(len(entities)):
            privilege = sum([(2**i)*int(j==str(True)) for i, j in enumerate(reversed(entities[x][1:len(entities[x]) - 1]))])
            elevationElements.append(ET.SubElement(elevationRoot, 'elevation', {'name': entities[x][0], 'privilege': str(privilege), 'elevationID': str(x)}))
            for sector in entities[x][len(entities[x]) - 1].split(','):
                sectorToElevation[sector] = x

        entities = self.ReadAll(self.storage['user'])
        for user in entities:
            parents = {}
            elevationID = None
            for sector in user[2].split(','):
                if sector in sectorToSubserver.keys():
                    parents[sector] = subserverElements[sectorToSubserver[sector]]
                if sector in sectorToRoom.keys():
                    parents[sector] = roomElements[sectorToRoom[sector]]
                if sector in sectorToElevation.keys():
                    if not elevationID:
                        elevationID = sectorToElevation[sector]
                    else:
                        logFunc(f"EXPORT FAILED: Elevation conflict on user '{user[0]}'.")
                        return
            if elevationID == None:
                logFunc(f"EXPORT FAILED: No elevation apllied to user '{user[0]}'.")
                return
            for sector, parent in parents.items():
                parentSectors = parent.get('sectors')
                if not parentSectors:
                    parentSectors = [sector]
                else:
                    parentSectors.append(parentSectors)
                parent.set('sectors', parentSectors)
                ET.SubElement(parent, 'user', {'username': user[0], 'password': user[1], 'elevationID': str(elevationID)})

        expFile.write(ET.tostring(root).decode())