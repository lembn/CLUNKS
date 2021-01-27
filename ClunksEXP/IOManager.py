import os
import pickle
import tempfile
from tkinter import messagebox

from ThreadingHelper import QUIT

USERS = 0
SUBSERVERS = 1
ROOMS = 2
ELEVATIONS = 3

class IOManager:
    def __init__(self):
        self._storage = [tempfile.TemporaryFile()] * 4

    def Cleanup(self):
        for f in self._storage:
            f.close()

    def Save(self, index, values):
        self._storage[index].seek(0)
        self._storage[index].write(pickle.dumps(values))

    def LoadSectors(self):
        users = self.LoadTemp(USERS)
        sectors = []
        for user in users:
            for sector in user[2].split(','):
                if sector.strip() not in sectors:
                    sectors.append(sector.strip())
        return sectors

    def LoadTemp(self, index):
        try:
            self._storage[index].seek(0)
            return pickle.loads(self._storage[index].read())
        except:
            messagebox.showerror('Oh no!', 'Data has been corrupted, please restart the program.')
            QUIT.set()

    def LoadExp(self):
        pass

    def Export(self):
        pass