import tkinter
from ttkthemes import themed_tk as tk
from tkinter import ttk
from PIL import ImageTk, Image
import threading

import IOManager as iom
from ThreadingHelper import STWThread, QUIT
from gui.CustomWidgets import TextArea
from gui.windows.UsersEditor import UsersEditor
from gui.windows.SubServersEditor import SubServersEditor
from gui.windows.RoomsEditor import RoomsEditor
from gui.windows.ElevationsEditor import ElevationsEditor

class MainWindow():
    def __init__(self, master, width, height):
        self.master = master
        self.width = width
        self.height = height
        self.master.SetupWindow(title='ClunksEXP', icon='gui/img/icon.ico', width=self.width, height=self.height, center=True, resizable=False, onClosing=self.Closing)
        self.master.protocol('WM_DELETE_WINDOW', self.Closing)
        self.Setup()

    def ApplySectors(self):
        pass

    def ResetUserEditor(self):self.userEditor = None; self.log.Append('Saved [users]')
    def ResetServerEditor(self):self.subserverEditor = None; self.log.Append('Saved [subservers]')
    def ResetRoomsEditor(self):self.roomsEditor = None; self.log.Append('Saved [rooms]')
    def ResetElevationEditor(self):self.elevationEditor = None; self.log.Append('Saved [elevations]')

    def OpenUserEditor(self):
        if not self.userEditor:
            self.userEditor = UsersEditor(self.master, 950, 340)
            self.watchUsersThread = STWThread(mainFunction=self.ResetUserEditor, waitFlags=[self.userEditor.closed], name="WatchUsersThread")
            self.watchUsersThread.start()
        else:
            self.userEditor.window.lift()

    def OpenSubServerEditor(self):
        if not self.subserverEditor:
            self.subserverEditor = SubServersEditor(self.master, 950, 340)
            self.watchServersThread = STWThread(mainFunction=self.ResetServerEditor, waitFlags=[self.subserverEditor.closed], name="WatchSubServersThread")
            self.watchServersThread.start()
        else:
            self.subserverEditor.window.lift()

    def OpenRoomsEditor(self):
        if not self.roomsEditor:
            self.roomsEditor = RoomsEditor(self.master, 950, 340)
            self.watchRoomsThread = STWThread(mainFunction=self.ResetRoomsEditor, waitFlags=[self.roomsEditor.closed], name="WatchRoomsThread")
            self.watchRoomsThread.start()
        else:
            self.roomsEditor.window.lift()

    def OpenElevationsEditor(self):
        if not self.elevationEditor:
            self.elevationEditor = ElevationsEditor(self.master, 950, 400)
            self.watchElevationsThread = STWThread(mainFunction=self.ResetElevationEditor, waitFlags=[self.elevationEditor.closed], name="WatchElevationsThread")
            self.watchElevationsThread.start()
        else:
            self.elevationEditor.window.lift()

    def Populate(self):
        self.contentFrame = ttk.Frame(self.master.container)
        self.contentFrame.pack(fill=tkinter.BOTH, expand=True)
        img = Image.open('gui/img/title.png')
        img.thumbnail([self.width, self.height/10], Image.ANTIALIAS)
        self.titleImg = ImageTk.PhotoImage(img)
        self.titleLbl = ttk.Label(self.contentFrame, image=self.titleImg)
        self.titleLbl.pack(padx=(10, 0), pady=15)
        self.topBtns = ttk.Frame(self.contentFrame)
        self.loadBtn = ttk.Button(self.topBtns, text='Load EXP', cursor='hand2', command=iom.Load, takefocus=False)
        self.usersBtn = ttk.Button(self.topBtns, text='Edit Users', cursor='hand2', command=self.OpenUserEditor, takefocus=False)
        self.serversBtn = ttk.Button(self.topBtns, text='Edit Sub-Servers', cursor='hand2', command=self.OpenSubServerEditor, takefocus=False)
        self.roomsBtn = ttk.Button(self.topBtns, text='Edit Rooms', cursor='hand2', command=self.OpenRoomsEditor, takefocus=False)
        self.elevationsBtn = ttk.Button(self.topBtns, text='Edit Elevations', cursor='hand2', command=self.OpenElevationsEditor, takefocus=False)
        self.logFrame = ttk.LabelFrame(self.contentFrame, text='Log')
        self.log = TextArea(self.logFrame, 20, 10)
        self.sectorsBtn = ttk.Button(self.contentFrame, text='Apply Sectors', cursor='hand2', command=self.ApplySectors, takefocus=False)
        self.exportBtn = ttk.Button(self.contentFrame, text='Export', cursor='hand2', command=iom.Export, takefocus=False)
        self.loadBtn.pack(padx=(0, 10), side=tkinter.LEFT)
        self.usersBtn.pack(padx=(0, 10), side=tkinter.LEFT)
        self.serversBtn.pack(padx=(0, 10), side=tkinter.LEFT)
        self.roomsBtn.pack(padx=(0, 10), side=tkinter.LEFT)
        self.elevationsBtn.pack(padx=(0, 10), side=tkinter.LEFT)
        self.topBtns.pack()
        self.log.pack()
        self.logFrame.pack(pady=(20, 6))
        self.sectorsBtn.pack(pady=10)
        self.exportBtn.pack()

    def Setup(self):
        self.elevationEditor = None
        self.userEditor = None
        self.subserverEditor = None
        self.roomsEditor = None
        self.Populate()

    def Closing(self):
        QUIT.set()
        self.master.destroy()