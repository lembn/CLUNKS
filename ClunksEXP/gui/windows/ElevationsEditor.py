import tkinter
from tkinter import messagebox
from ttkthemes import themed_tk as tk
from tkinter import ttk
import threading

import gui.CustomWidgets as cw

class ElevationsEditor(cw.Editor):
    OPTIONS = {'Name': 35, 'Call Subservers': 75, 'Call Rooms': 55, 'Call Groups': 55, 'Call User': 50, 'Message Subserver': 95, 'Message Rooms': 80, 
               'Message Groups': 80, 'Message User': 80, 'Create Groups': 70, 'Sectors': 35}

    def __init__(self, master, width, height):
        self.width = width
        self.height = height
        self.window = master.AddWindow(title='ClunksEXP - Elevations', icon='gui/img/icon.ico', width=self.width, height=self.height, center=True, resizable=False)
        self.window.protocol('WM_DELETE_WINDOW', super().Closing)
        self.style = ttk.Style(self.window)
        self.style.configure('Placeholder.TEntry', foreground='#d5d5d5')
        self.Populate()
        super().__init__(self.window, self.OPTIONS)

    def OnTreeViewClick(self, event):
        if self.treeView.identify_region(event.x, event.y) == 'separator':
            return 'break'

    def New(self):
        if not self.nameEntry.get().strip() or not self.sectorsEntry.get().strip():
            messagebox.showwarning('Missing Data', 'Please fill in all feilds')
            return
        if self.nameEntry.get().strip() == 'Name' or self.sectorsEntry.get().strip() == 'Sectors':
            messagebox.showwarning('Missing or Invalid Data', "Please fill in all feilds. You cannot leave 'Name' or 'Sectors' empty.")
            return
        values = [self.nameEntry.get().strip()]
        for btn in self.checkBtns:
            values.append(str(bool(btn.variable.get())))
            btn.variable.set(0)
        values.append(self.sectorsEntry.get().strip())
        self.nameEntry.Reset()
        self.sectorsEntry.Reset()
        self.window.focus()
        for child in self.treeView.get_children():
            if self.treeView.item(child)['values'][0] == values[0]:
                messagebox.showwarning('Invalid Data', f"Entry with {self.options[0].lower()} '{values[0]}' already exists.")
                return
        self.items += 1
        self.treeView.insert('', tkinter.END, self.items, text='', values=tuple(values))

    def Populate(self):
        self.contentFrame = ttk.Frame(self.window)
        self.contentFrame.pack(fill=tkinter.BOTH, expand=True)
        #Treeview
        self.treeView = cw.ScrollableTreeView(self.contentFrame, tuple(self.OPTIONS.keys()))
        for option in self.OPTIONS:
            self.treeView.column(option, anchor=tkinter.CENTER, width=self.OPTIONS[option], minwidth=self.OPTIONS[option])
            self.treeView.heading(option, text=option, anchor=tkinter.CENTER)
        self.treeView.bind('<Button-1>', self.OnTreeViewClick)
        self.treeView.pack(fill=tkinter.BOTH)
        #New Elevation
        self.creatorContainer = ttk.Frame(self.contentFrame)
        self.creatorFrame = ttk.LabelFrame(self.creatorContainer, text='Create...')
        self.creatorComponentContainer = ttk.Frame(self.creatorFrame)
        self.newTop = ttk.Label(self.creatorComponentContainer)
        self.checkBtns = []
        optKeys = list(self.OPTIONS.keys())
        for x in range(1, len(optKeys) - 1):
            self.checkBtns.append(cw.LabeledCheckbutton(self.newTop, optKeys[x]))
        for btn in self.checkBtns:
            padding = (10, 0)
            if btn == self.checkBtns[len(self.checkBtns ) - 1]:
                padding = (10, 10)
            btn.pack(padx=padding, side=tkinter.LEFT)
        self.newBottom = ttk.Frame(self.creatorComponentContainer)
        self.nameEntry = cw.PlaceholderEntry(self.newBottom, 'Name')
        self.sectorsEntry = cw.PlaceholderEntry(self.newBottom, 'Sectors')
        self.newBtn = ttk.Button(self.newBottom, text='Add New', cursor='hand2', command=self.New, takefocus=False)
        self.nameEntry.pack(side=tkinter.LEFT)
        self.sectorsEntry.pack(padx=10, side=tkinter.LEFT)
        self.newBtn.pack()
        self.newTop.pack(pady=(10, 0), side=tkinter.TOP)
        self.newBottom.pack(pady=10, side=tkinter.BOTTOM)
        self.creatorContainer.pack()
        self.creatorFrame.pack()
        self.creatorComponentContainer.pack()
        #Remove
        self.removeBtn = ttk.Button(self.newBottom, text='Remove', cursor='hand2', command=self.Remove, takefocus=False)
        self.removeBtn.pack(pady=(10, 0))