import tkinter
from ttkthemes import themed_tk as tk
from tkinter import ttk
import threading

import gui.CustomWidgets as cw
from IOManager import SUBSERVERS

class SubServersEditor(cw.Editor):
    OPTIONS = ('Sub-Server Name', 'Sectors')

    def __init__(self, master, width, height):
        self.width = width
        self.height = height
        self.window = master.AddWindow(title='ClunksEXP - SubServers', icon='gui/img/icon.ico', width=self.width, height=self.height, center=True, resizable=False)
        self.window.protocol('WM_DELETE_WINDOW', super().Closing)
        self.style = ttk.Style(self.window)
        self.style.configure('Placeholder.TEntry', foreground='#d5d5d5')
        super().__init__(SUBSERVERS, self.window, self.OPTIONS)
        super().Populate()