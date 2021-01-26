import tkinter
from ttkthemes import themed_tk as tk
from tkinter import ttk
import threading

import gui.CustomWidgets as cw
from ThreadingHelper import QUIT

class UsersEditor(cw.Editor):
    OPTIONS = ('Username', 'Password', 'Sectors')

    def __init__(self, master, width, height):
        self.width = width
        self.height = height
        self.window = master.AddWindow(title='ClunksEXP - Users', icon='gui/img/icon.ico', width=self.width, height=self.height, center=True, resizable=False)#, onClosing=self.Closing)
        self.window.protocol('WM_DELETE_WINDOW', self.Closing)
        self.style = ttk.Style(self.window)
        self.style.configure('Placeholder.TEntry', foreground='#d5d5d5')
        self.Populate()
        super().__init__(0, self.treeView, self.window, entries=self.entries)

    def Populate(self):
        self.contentFrame = ttk.Frame(self.window)
        self.contentFrame.pack(fill=tkinter.BOTH, expand=True)
        #Treeview
        self.treeView = cw.ScrollableTreeView(self.contentFrame, self.OPTIONS)
        for option in self.OPTIONS:
            self.treeView.column(option, anchor=tkinter.CENTER, width=70, minwidth=60)
            self.treeView.heading(option, text=option, anchor=tkinter.CENTER)
        self.treeView.pack(fill=tkinter.BOTH)
        #New User
        self.creatorContainer = ttk.Frame(self.contentFrame)
        self.creatorFrame = ttk.LabelFrame(self.creatorContainer, text='Create...')
        self.creatorComponentContainer = ttk.Frame(self.creatorFrame)
        self.newTop = ttk.Label(self.creatorComponentContainer)
        self.entries = []
        for option in self.OPTIONS:
            self.entries.append(cw.PlaceholderEntry(self.newTop, option))
        for entry in self.entries:
            padding = (10, 0)
            if entry == self.entries[len(self.entries) - 1]:
                padding = (10, 10)
            entry.pack(padx=padding, side=tkinter.LEFT)
        self.newBottom = ttk.Frame(self.creatorComponentContainer)
        self.newBtn = ttk.Button(self.newBottom, text='Add New', cursor='hand2', command=self.New, takefocus=False)
        self.newBtn.pack(side=tkinter.LEFT)
        self.newTop.pack(pady=(10, 0), side=tkinter.TOP)
        self.newBottom.pack(pady=10, side=tkinter.BOTTOM)
        self.creatorContainer.pack()
        self.creatorFrame.pack()
        self.creatorComponentContainer.pack()
        #Remove
        self.removeBtn = ttk.Button(self.newBottom, text='Remove', cursor='hand2', command=self.Remove, takefocus=False)
        self.removeBtn.pack()