import tkinter
from ttkthemes import themed_tk as tk
from tkinter import ttk
from PIL import ImageTk, Image
import threading

import Helper
import IOManager as iom
from gui.windows.ServersWindow import ServersWindow
from gui.windows.RoomsWindow import RoomsWindow
from gui.windows.ElevationsWindow import ElevationsWindow

class MainWindow():
    def __init__(self, master, width, height):
        self.master = master
        self.width = width
        self.height = height
        self.master.SetupWindow(title='ClunksEXP', width=self.width, height=self.height, center=True, resizable=False)
        self.Setup()

    def AddTitle(self):
        return
        img = Image.open('gui/img/title.png')
        img.thumbnail(size, Image.ANTIALIAS)
        self.titleImg = ImageTk.PhotoImage(img)
        self.titleLbl = ttk.Label(self.contentFrame, image=self.titleImg)
        self.titleLbl.pack(padx=(10, 0), pady=15)

    def AddButtons(self):
        self.contentFrame = ttk.Frame(self.master.container)
        self.loadBtn = ttk.Button(self.contentFrame, text='Load Users', cursor='hand2', command=iom.Load, takefocus=False)
        self.serversBtn = ttk.Button(self.contentFrame, text='Create Servers', cursor='hand2', command=self.OpenServersWndw, takefocus=False)
        self.roomsBtn = ttk.Button(self.contentFrame, text='Create Rooms', cursor='hand2', command=self.OpenRoomsWndw, takefocus=False)
        self.elevationsBtn = ttk.Button(self.contentFrame, text='Create Elevations', cursor='hand2', command=self.OpenElevationsWndw, takefocus=False)
        self.exportBtn = ttk.Button(self.contentFrame, text='Export', cursor='hand2', command=iom.Export, takefocus=False)
        self.contentFrame.pack(fill=tkinter.BOTH, expand=True)
        self.loadBtn.pack(pady=(15, 0))
        self.serversBtn.pack(pady=(10, 0))
        self.roomsBtn.pack(pady=(10, 0))
        self.elevationsBtn.pack(pady=(10, 0))
        self.exportBtn.pack(pady=10)

    def ResetServersWndw(self):
        self.serversWndw = None

    def ResetRoomsWndw(self):
        self.roomsWndw = None

    def ResetElevationsWndw(self):
        self.elevationsWndw = None

    def OpenServersWndw(self):
        if not self.serversWndw:
            self.serversWndw = ServersWindow(self.master, 500, 300)
            self.watchServersWndw = Helper.STWThread(mainFunction=self.ResetServersWndw, waitFlags=[self.serversWndw.closed], name='WatchServersWndw')
            self.watchServersWndw.start()
        else:
            self.serversWndw.window.lift()

    def OpenRoomsWndw(self):
        if not self.roomsWndw:
            self.roomsWndw = RoomsWindow(self.master, 300, 300)
            self.watchRoomsWndw = Helper.STWThread(mainFunction=self.ResetRoomsWndw, waitFlags=[self.roomsWndw.closed], name='WatchServersWndw')
            self.watchRoomsWndw.start()
        else:
            self.roomsWndw.window.lift()

    def OpenElevationsWndw(self):
        if not self.elevationsWndw:
            self.elevationsWndw = ElevationsWindow(self.master, 600, 400)
            self.watchElevationsWndw = Helper.STWThread(mainFunction=self.ResetElevationsWndw, waitFlags=[self.elevationsWndw.closed], name='WatchServersWndw')
            self.watchElevationsWndw.start()
        else:
            self.elevationsWndw.window.lift()

    def Setup(self):
        self.serversWndw = None
        self.roomsWndw = None
        self.elevationsWndw = None
        self.AddTitle()
        self.AddButtons()