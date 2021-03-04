import bcrypt
import tkinter
from tkinter import ttk
from tkinter import messagebox
from ttkthemes import themed_tk as tk
import threading

import gui.CustomWidgets as cw

class RoomsEditor(cw.Editor):
    OPTIONS = ('Room Name', 'Password', 'Parent', 'Sectors')

    def __init__(self, master, width, height):
        self.width = width
        self.height = height
        self.window = master.AddWindow(title='ClunksEXP - SubServers', icon='gui/img/icon.ico', width=self.width, height=self.height, center=True, resizable=False)
        self.window.protocol('WM_DELETE_WINDOW', super().Closing)
        self.style = ttk.Style(self.window)
        self.style.configure('Placeholder.TEntry', foreground='#d5d5d5')
        super().__init__(self.window, self.OPTIONS)
        super().Populate()

    def New(self):
        values = []
        for entry in self.entries: 
            if entry.get().strip() == entry.placeholder:
                values.append('')
                continue
            values.append(entry.get().strip())
        for entry in self.entries:
            entry.Reset()
        self.window.focus()
        if values[1]:
            values[1] = bcrypt.hashpw(values[1].encode('utf-8'), bcrypt.gensalt()).decode('utf-8')
        for child in self.treeView.get_children():
            if self.treeView.item(child)['values'][0] == values[0]:
                messagebox.showwarning('Invalid Data', f"Entry with {self.options[0].lower()} '{values[0]}' already exists.")
                return
        self.items += 1
        self.treeView.insert('', tkinter.END, self.items, text='', values=tuple(values))