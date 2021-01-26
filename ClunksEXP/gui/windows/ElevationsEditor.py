import tkinter
from tkinter import messagebox
from ttkthemes import themed_tk as tk
from tkinter import ttk
import threading

import gui.CustomWidgets as cw
from Globals import elevations

class ElevationsEditor:
    OPTIONS = {'Name': 32, 'Call Subservers': 75, 'Call Rooms': 55, 'Call Groups': 55, 'Message Subserver': 95, 'Message Rooms': 80, 
               'Message Groups': 80, 'Create Subservers': 90, 'Create Rooms': 70, 'Create Groups': 70, 'Load Exp': 40}
    items = 0

    def __init__(self, master, width, height):
        self.width = width
        self.height = height
        self.window = master.AddWindow(title='ClunksEXP - Elevations', icon='gui/img/icon.ico', width=self.width, height=self.height, center=True, resizable=False, onClosing=self.Closing)
        self.style = ttk.Style(self.window)
        self.style.configure('Placeholder.TEntry', foreground='#d5d5d5')
        self.close = threading.Event()
        self.Populate()

    def OnTreeViewClick(self, event):
        if self.treeView.identify_region(event.x, event.y) == 'separator':
            return 'break'

    def New(self):
        if not self.nameEntry.get().strip():
            messagebox.showwarning('Missing Data', 'Please fill in the name feild')
            return
        if self.nameEntry.get().strip() == 'Name':
            messagebox.showwarning('Invalid Data', 'Please choose a different name')
            return
        values = [self.nameEntry.get().strip()]
        for btn in self.checkBtns:
            values.append(str(bool(btn.variable.get())))
            btn.variable.set(0)
        self.nameEntry.Reset()
        self.window.focus()
        self.items += 1
        self.treeView.insert('', tkinter.END, self.items, text='', values=tuple(values))

    def Remove(self):
        self.items -= len(self.treeView.selection())
        for item in self.treeView.selection():
            self.treeView.delete(item)

    def Populate(self):
        self.contentFrame = ttk.Frame(self.window)
        self.contentFrame.pack(fill=tkinter.BOTH, expand=True)
        #Treeview
        self.treeViewContainer = ttk.Frame(self.contentFrame)
        self.treeViewScroll = cw.AutoScrollbar(self.treeViewContainer, orient='vertical')
        self.treeView = ttk.Treeview(self.treeViewContainer, yscrollcommand=self.treeViewScroll.set)
        self.treeViewScroll.configure(command=self.treeView.yview)
        self.treeView.configure(columns=tuple(self.OPTIONS.keys()))
        self.treeView.column('#0', width=0, stretch=tkinter.NO)
        self.treeView.heading('#0', text='')
        for option in self.OPTIONS:
            self.treeView.column(option, anchor=tkinter.CENTER, width=self.OPTIONS[option], minwidth=self.OPTIONS[option])
            self.treeView.heading(option, text=option, anchor=tkinter.CENTER)
        self.treeView.bind('<Button-1>', self.OnTreeViewClick)
        self.treeView.grid(row=0, column=0, sticky=tkinter.NSEW)
        self.treeViewScroll.grid(row=0, column=1, sticky=tkinter.NS)
        self.treeViewContainer.grid_rowconfigure(0, weight=1)
        self.treeViewContainer.grid_columnconfigure(0, weight=1)
        self.treeViewContainer.pack(fill=tkinter.BOTH)
        #New Elevation
        self.creatorContainer = ttk.Frame(self.contentFrame)
        self.creatorFrame = ttk.LabelFrame(self.creatorContainer, text='Create...')
        self.creatorComponentContainer = ttk.Frame(self.creatorFrame)
        self.newTop = ttk.Label(self.creatorComponentContainer)
        self.checkBtns = []
        for option in self.OPTIONS:
            self.checkBtns.append(cw.LabeledCheckbutton(self.newTop, option))
        for btn in self.checkBtns:
            padding = (10, 0)
            if btn == self.checkBtns[len(self.checkBtns ) - 1]:
                padding = (10, 10)
            btn.pack(padx=padding, side=tkinter.LEFT)
        self.newBottom = ttk.Frame(self.creatorComponentContainer)
        self.nameEntry = cw.PlaceholderEntry(self.newBottom, 'Name')
        self.newBtn = ttk.Button(self.newBottom, text='Add New', cursor='hand2', command=self.New, takefocus=False)
        self.nameEntry.pack(padx=(0, 10), side=tkinter.LEFT)
        self.newBtn.pack()
        self.newTop.pack(pady=(10, 0), side=tkinter.TOP)
        self.newBottom.pack(pady=10, side=tkinter.BOTTOM)
        self.creatorContainer.pack()
        self.creatorFrame.pack()
        self.creatorComponentContainer.pack()
        #Remove
        self.removeBtn = ttk.Button(self.newBottom, text='Remove', cursor='hand2', command=self.Remove, takefocus=False)
        self.removeBtn.pack(pady=(10, 0))

    def Closing(self):
        elevations = self.treeView.get_children()
        self.close.set()
        self.window.destroy()