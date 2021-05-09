import bcrypt
import tkinter
from tkinter import ttk
from tkinter import messagebox
from ttkthemes import themed_tk as tk
import threading

import gui.CustomWidgets as cw

class UsersEditor(cw.Editor):
    OPTIONS = ('Username', 'Password', 'Sectors', 'Global')

    def __init__(self, master, width, height):
        self.width = width
        self.height = height
        self.window = master.AddWindow(title='ClunksEXP - Users', icon=cw.RelToAbs('gui/img/icon.ico'), width=self.width, height=self.height, center=True, resizable=False)
        self.window.protocol('WM_DELETE_WINDOW', super().Closing)
        self.style = ttk.Style(self.window)
        self.style.configure('Placeholder.TEntry', foreground='#d5d5d5')
        super().__init__(self.window, self.OPTIONS)
        super().Populate(include=3)
        #Add Global checkbutton
        self.isGlobal = cw.LabeledCheckbutton(self.newTop, self.OPTIONS[3])
        self.isGlobal.pack(padx=(10, 10), side=tkinter.LEFT)
        #Add Global to treeview
        self.treeView.pack_forget()
        self.treeView.column(self.OPTIONS[3], anchor=tkinter.CENTER, width=30, minwidth=30)
        self.treeView.heading(self.OPTIONS[3], text=self.OPTIONS[3], anchor=tkinter.CENTER)
        self.treeView.pack(fill=tkinter.BOTH)

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
        values.append(str(bool(self.isGlobal.variable.get())))
        self.isGlobal.variable.set(0)
        for child in self.treeView.get_children():
            if self.treeView.item(child)['values'][0] == values[0]:
                messagebox.showwarning('Invalid Data', f"Entry with {self.options[0].lower()} '{values[0]}' already exists.")
                return
        self.items += 1
        self.treeView.insert('', tkinter.END, self.items, text='', values=tuple(values))