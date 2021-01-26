import tkinter
from tkinter import ttk
from tkinter import messagebox
from ttkthemes import themed_tk as tk
import threading

from Globals import data

class RootWindow(tk.ThemedTk):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.container = ttk.Frame(self)
        self.container.pack(expand=True, fill=tkinter.BOTH)

    def SetupWindow(self, **kwargs):
        if 'window' not in kwargs.keys():
            kwargs['window'] = self
        if 'title' in kwargs.keys():
            kwargs['window'].title(kwargs['title'])
        if 'icon' in kwargs.keys():
            kwargs['window'].iconbitmap(kwargs['icon'])
        if 'width' in kwargs.keys() and 'height' in kwargs.keys() and 'center' in kwargs.keys():
            if kwargs['center']:
                screenWidth = kwargs['window'].winfo_screenwidth()
                screenHeight = kwargs['window'].winfo_screenheight()
                x = (screenWidth/2) - (kwargs['width']/2)
                y = (screenHeight/2) - (kwargs['height']/2)
                kwargs['window'].geometry(f"{kwargs['width']}x{kwargs['height']}+{int(x)}+{int(y)}")
            else:
                pass
        if 'resizable' in kwargs.keys():
            if not kwargs['resizable']:
                kwargs['window'].resizable(False, False)

    def AddWindow(self, **kwargs):
        window = tkinter.Toplevel(self)
        kwargs['window'] = window
        self.SetupWindow(**kwargs)
        return window

    def Clear(self):
        self.container.destroy()
        self.container = ttk.Frame(self)
        self.container.pack(expand=True, fill=tkinter.BOTH)

class AutoScrollbar(ttk.Scrollbar):
    def set(self, low, high):
        if float(low) <= 0.0 and float(high) >= 1.0:
            self.tk.call('grid', 'remove', self)
        else:
            self.grid()
        ttk.Scrollbar.set(self, low, high)

    def pack(self, **kwargs):
        raise tkinter.TclError('pack cannot be used with this widget')

    def place(self, **kwargs): 
        raise tkinter.TclError('place cannot be used  with this widget') 

class TextArea(tkinter.Text):
    def __init__(self, master, width, height, **kwargs):
        self.master = master
        self.container = ttk.Frame(self.master)
        super().__init__(self.container, width=width, height=height, wrap='none', borderwidth=0, name=str(kwargs.pop('ID', '0')), undo=True)
        self.textVsb = AutoScrollbar(self.container, orient='vertical', command=self.yview)
        self.textHsb = AutoScrollbar(self.container, orient='horizontal', command=self.xview)
        self.configure(state=tkinter.DISABLED)
        self.configure(yscrollcommand=self.textVsb.set, xscrollcommand=self.textHsb.set)
        self.configure(background='grey')
        self.configure(foreground='white')

    def SelectAll(self, event):
        event.widget.tag_add('sel','1.0','end')

    def pack(self, padx=(0, 0), pady=(0, 0)):
        self.grid(row=0, column=0, sticky=tkinter.NSEW)
        self.textVsb.grid(row=0, column=1, sticky=tkinter.NS)
        self.textHsb.grid(row=1, column=0, sticky=tkinter.EW)
        self.container.grid_rowconfigure(0, weight=1)
        self.container.grid_columnconfigure(0, weight=1)
        self.container.pack(side='top', padx=padx, pady=pady)

    def Clear(self):
        self.config(state=tkinter.NORMAL)
        self.delete(1.0, tkinter.END)
        self.config(state=tkinter.DISABLED)

    def Append(self, text):
        self.config(state=tkinter.NORMAL)
        self.insert(tkinter.END, text)
        self.config(state=tkinter.DISABLED)

class PlaceholderEntry(ttk.Entry):
    def __init__(self, master, placeholder, *args, **kwargs):
        super().__init__(master, *args, style='Placeholder.TEntry', **kwargs)
        self.placeholder = placeholder
        self.insert(0, self.placeholder)
        self.bind("<FocusIn>", self.In)
        self.bind("<FocusOut>", self.Out)

    def In(self, event):
        if self['style'] == 'Placeholder.TEntry':
            self.delete(0, tkinter.END)
            self['style'] = 'TEntry'

    def Out(self, event):
        if not self.get():
            self['show'] = ''
            self.insert(0, self.placeholder)
            self['style'] = 'Placeholder.TEntry'

    def Reset(self):
        self['style'] = 'Placeholder.TEntry'
        self['show'] = ''
        self.delete(0, tkinter.END)
        self.insert(0, self.placeholder)

class LabeledCheckbutton(ttk.Frame):
    def __init__(self, master, text):
        super().__init__(master)
        self.variable = tkinter.IntVar()
        self.checkbutton = ttk.Checkbutton(self, variable=self.variable, takefocus=False)
        self.label = ttk.Label(self, text=text)
        self.checkbutton.pack()
        self.label.pack(side=tkinter.BOTTOM)

class ScrollableTreeView(ttk.Treeview):
    def __init__(self, master, options, *args, **kwargs):
        self.container = ttk.Frame(master)
        self.scroll = AutoScrollbar(self.container, orient='vertical')
        super().__init__(self.container, *args, yscrollcommand=self.scroll.set, **kwargs)
        self.scroll.configure(command=self.yview)
        self.configure(columns=options)
        self.column('#0', width=0, stretch=tkinter.NO)
        self.heading('#0', text='')

    def pack(self, **kwargs):
        self.grid(row=0, column=0, sticky=tkinter.NSEW)
        self.scroll.grid(row=0, column=1, sticky=tkinter.NS)
        self.container.grid_rowconfigure(0, weight=1)
        self.container.grid_columnconfigure(0, weight=1)
        self.container.pack(**kwargs)

class Editor:
    def __init__(self, dataIndex, treeView, window, **kwargs):
        self.dataIndex = dataIndex
        self.treeView = treeView
        self.window = window
        self.entries = kwargs.pop('entries', [])
        self.closed = threading.Event()
        self.items = 0

    def New(self):
        values = []
        for entry in self.entries:
            if not entry.get().strip():
                messagebox.showwarning('Missing Data', f'Please fill in the {entry.placeholder.lower()} feild')
                return
            if entry.get().strip() == entry.placeholder:
                messagebox.showwarning('Invalid Data', f'Please choose a different {entry.placeholder.lower()}')
                return
            values.append(entry.get().strip())
            entry.Reset()
        self.window.focus()
        self.treeView.insert('', tkinter.END, self.items, text='', values=tuple(values))

    def Remove(self):
        self.items -= len(self.treeView.selection())
        for item in self.treeView.selection():
            self.treeView.delete(item)

    def Closing(self):
        try:
            data[self.dataIndex] = self.treeView.get_children()
        except:
            pass
        self.closed.set()
        self.window.destroy()