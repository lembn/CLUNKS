import tkinter
from ttkthemes import themed_tk as tk
from tkinter import ttk
from PIL import ImageTk, Image
import threading

from ThreadingHelper import STWThread, QUIT
import IOManager as iom
from gui.CustomWidgets import TextArea
from gui.windows.ElevationsEditor import ElevationsEditor

class MainWindow():
    def __init__(self, master, width, height):
        self.master = master
        self.width = width
        self.height = height
        self.master.SetupWindow(title='ClunksEXP', icon='gui/img/icon.ico', width=self.width, height=self.height, center=True, resizable=False, onClosing=self.Closing)
        self.master.protocol("WM_DELETE_WINDOW", self.Closing)
        self.Setup()

    def ResetElevationsEditor(self):
        self.elevationEditor = None

    def OpenElevationsEditor(self):
        if not self.elevationEditor:
            self.elevationEditor = ElevationsEditor(self.master, 950, 400)
            self.watchWindowThread = STWThread(mainFunction=self.ResetElevationsEditor, waitFlags=[self.elevationEditor.closed], name="WatchWindowThread")
            self.watchWindowThread.start()
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
        self.usersBtn = ttk.Button(self.topBtns, text='Load Users', cursor='hand2', command=iom.Load, takefocus=False)
        self.serversBtn = ttk.Button(self.topBtns, text='Load Servers', cursor='hand2', command=iom.Load, takefocus=False)
        self.roomsBtn = ttk.Button(self.topBtns, text='Load Rooms', cursor='hand2', command=iom.Load, takefocus=False)
        self.elevationsBtn = ttk.Button(self.topBtns, text='Edit Elevations', cursor='hand2', command=self.OpenElevationsEditor, takefocus=False)
        self.logFrame = ttk.LabelFrame(self.contentFrame, text='Log')
        self.log = TextArea(self.logFrame, 20, 10)
        self.exportBtn = ttk.Button(self.contentFrame, text='Export', cursor='hand2', command=iom.Export, takefocus=False)
        self.loadBtn.pack(padx=(0, 10), side=tkinter.LEFT)
        self.usersBtn.pack(padx=(0, 10), side=tkinter.LEFT)
        self.serversBtn.pack(padx=(0, 10), side=tkinter.LEFT)
        self.roomsBtn.pack(padx=(0, 10), side=tkinter.LEFT)
        self.elevationsBtn.pack(padx=(0, 10), side=tkinter.LEFT)
        self.topBtns.pack()
        self.log.pack()
        self.logFrame.pack(pady=20)
        self.exportBtn.pack(pady=20)

    def Setup(self):
        self.elevationEditor = None
        self.Populate()

    def Closing(self):
        QUIT.set()
        self.master.destroy()