import tkinter
from ttkthemes import themed_tk as tk
from tkinter import ttk

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
        if 'onClosing' in kwargs.keys():
            self.protocol('WM_DELETE_WINDOW', kwargs['onClosing'])

    def AddWindow(self, **kwargs):
        window = tkinter.Toplevel(self)
        kwargs['window'] = window
        self.SetupWindow(**kwargs)
        return window

    def Clear(self):
        self.container.destroy()
        self.container = ttk.Frame(self)
        self.container.pack(expand=True, fill=tkinter.BOTH)

    def Destroy(self):
        self.destroy()

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
        self.readonly = kwargs.pop('readonly', False)
        super().__init__(self.container, width=width, height=height, wrap='none', borderwidth=0, name=str(kwargs.pop('ID', '0')), undo=True)
        if self.readonly:
            self.configure(state=tkinter.DISABLED)
        self.textVsb = AutoScrollbar(self.container, orient='vertical', command=self.yview)
        self.textHsb = AutoScrollbar(self.container, orient='horizontal', command=self.xview)
        self.configure(yscrollcommand=self.textVsb.set, xscrollcommand=self.textHsb.set)
        self.bind('Text','<Control-a>', self.SelectAll)
        if 'save' in kwargs.keys():
            self.bind('<Control-s>', kwargs['save'])
        if 'jig' in kwargs.keys():
            self.bind('<Control-j>', kwargs['jig'])
        if theme == DARK:
            self.configure(background='grey')
            self.configure(foreground='white')

    def SelectAll(self, event):
        event.widget.tag_add('sel','1.0','end')

    def Pack(self, padx=(0, 0), pady=(0, 0)):
        self.grid(row=0, column=0, sticky=tkinter.NSEW)
        self.textVsb.grid(row=0, column=1, sticky=tkinter.NS)
        self.textHsb.grid(row=1, column=0, sticky=tkinter.EW)
        self.container.grid_rowconfigure(0, weight=1)
        self.container.grid_columnconfigure(0, weight=1)
        self.container.pack(side='top', fill='both', expand=True, padx=padx, pady=pady)

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
        self.isPassword = False
        if 'password' in kwargs.keys():
            self.isPassword = True
            del kwargs['password']
        super().__init__(master, *args, style='Placeholder.TEntry', **kwargs)
        self.placeholder = placeholder
        self.insert(0, self.placeholder)
        self.bind("<FocusIn>", self.In)
        self.bind("<FocusOut>", self.Out)

    def In(self, event):
        if self['style'] == 'Placeholder.TEntry':
            if self.isPassword:
                self['show'] = '•'
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

class ProductWidget:
    def __init__(self, master, product, editable=True):
        self.master = master
        self.container = ttk.Frame(self.master)
        self.nameLbl = ttk.Label(self.container, text=product.name)
        if theme == DARK:
            self.netImg = GetImageTk('internet', [15,15], invert=True)
            if editable:
                self.tickImg = GetImageTk('tick', [15,15], invert=True)
                self.addImg = GetImageTk('add', [15,15], invert=True)
                self.removeImg = GetImageTk('remove', [15,15], invert=True)
        else:
            self.netImg = GetImageTk('internet', [15,15])
            if editable:
                self.tickImg = GetImageTk('tick', [15,15])
                self.addImg = GetImageTk('add', [15,15])
                self.removeImg = GetImageTk('remove', [15,15])
        self.netBtn = ttk.Button(self.container, image=self.netImg, cursor='hand2', takefocus=False, command=lambda:webbrowser.open(product.link, new=2, autoraise=True))
        self.DPLbl = ttk.Label(self.container, text=f"{product.DT}\n£{format(product.price, '.2f')}")
        if editable:
            self.tickLbl = ttk.Label(self.container, image=self.tickImg)
            self.addBtn = ttk.Button(self.container, image=self.addImg, cursor='hand2', takefocus=False, command=lambda:cart.Add(product, self.tickLbl))
            self.removeBtn = ttk.Button(self.container, image=self.removeImg, cursor='hand2', takefocus=False, command=lambda:cart.Remove(product, self.tickLbl))
            self.nameLbl.pack(side=tkinter.LEFT, padx=(0, 10))
            self.netBtn.pack(side=tkinter.LEFT, padx=(0, 10))
            self.DPLbl.pack(side=tkinter.LEFT, padx=(0, 10))
            self.addBtn.pack(side=tkinter.LEFT, padx=(0, 10))
            self.removeBtn.pack(side=tkinter.LEFT, padx=(0, 10))
            if product in cart:
                self.tickLbl.pack(pady=(10, 0))
        else:
            self.nameLbl.pack(side=tkinter.LEFT, padx=(0, 10))
            self.netBtn.pack(side=tkinter.LEFT, padx=(0, 10))
            self.DPLbl.pack(side=tkinter.LEFT, padx=(0, 10))

class ScrollableFrame(ttk.Frame):
    def __init__(self, master, width, height, *args, **kwargs):
        self.master = master
        self.width = width
        self.height = height
        self.withLoading = kwargs.pop('withLoading', False)
        super().__init__(self.master, *args, **kwargs)
        if theme == DARK:
            self.canvas = tkinter.Canvas(self, background='gray')
        else:
            self.canvas = tkinter.Canvas(self)
        self.scroll = AutoScrollbar(self, orient='vertical', command=self.canvas.yview)
        self.contentFrame = ttk.Frame(self.canvas)
        self.bind('<Configure>', lambda e: self.canvas.configure(scrollregion=self.canvas.bbox('all')))
        self.canvas.create_window((0, 0), window=self.contentFrame, anchor=tkinter.NW, width=self.width, height=self.height)
        self.canvas.configure(yscrollcommand=self.scroll.set)
        if self.withLoading:
            self.loadingLbl = ttk.Label(self.contentFrame, text='Loading...')
            self.loadingBar = ttk.Progressbar(self.contentFrame, orient=tkinter.HORIZONTAL, length=self.width - 100, mode='indeterminate')
        self.grid_rowconfigure(0, weight=1)
        self.grid_columnconfigure(1, weight=1)
        self.canvas.grid(row=0, column=0, sticky=tkinter.NSEW, columnspan=2)
        self.scroll.grid(row=0, column=2, sticky=tkinter.NS)

    def StartLoading(self):
        if self.withLoading:
            self.loadingLbl.pack()
            self.loadingBar.pack()
            self.loadingBar.start(20)

    def StopLoading(self):
        if self.withLoading:
            self.loadingBar.stop()
            self.loadingLbl.pack_forget()
            self.loadingBar.pack_forget()

    def ResetCWSize(self):
        """
        Reset CanvasWindow Size
        Used for situations where the canvas window should fill the size of the
        ScrollableFrame's master widget, but the widget requires the ScrollableFrame
        to be put in before it will be at its maximum size. (Eg frames - empty when intialised,
        but grow to fit content). This allows the ScrollableFrame to be submitted with an empty
        canvas, then the canvas can grow to fit afterwards by calling this function.
        """
        self.master.update()
        self.canvas.create_window((0, 0), window=self.contentFrame, anchor=tkinter.NW, width=self.master.winfo_width(), height=self.master.winfo_height())

class DefaultValueEntry(ttk.Frame):
    def __init__(self, master, *args, **kwargs):
        self._text = kwargs.pop('text', '')
        self.default = kwargs.pop('default', None)
        self._func = kwargs.pop('func', lambda: None)
        self._width = kwargs.pop('width', lambda: 5)
        self._justify = kwargs.pop('justify', tkinter.CENTER)
        self._validate = kwargs.pop('validate', True)
        super().__init__(master, *args, **kwargs)
        self.label = ttk.Label(self, text=self._text)
        self.entry = ttk.Entry(self, width=self._width, cursor='xterm', takefocus=False, justify=self._justify)
        self.insert(self.default)
        self.entry.bind('<FocusOut>', self.OnLeave)
        self.entry.bind('<Return>', self.OnLeave)

    def OnLeave(self, event):
        if self._validate:
            value = self.get().strip()
            if not value:
                self.reset()
                return
            try:
                i = int(value)
            except ValueError:
                self.reset()
                return
        self._func()

    def pack(self, *args, **kwargs):
        super().pack(padx=kwargs.pop('padx', 20), pady=kwargs.pop('pady', (5, 5)))
        self.label.pack(side=tkinter.LEFT, padx=(0, 5))
        self.entry.pack()

    def get(self):
        return self.entry.get()

    def insert(self, value):
        self.delete()
        self.entry.insert(0, value)

    def delete(self):
        self.entry.delete(0, tkinter.END)

    def reset(self):
        self.delete()
        self.insert(self.default)

class ValueSlider(ttk.Frame):
    def __init__(self, master, *args, **kwargs):
        self._text = kwargs.pop('text', '')
        self._orient = kwargs.pop('orient', tkinter.HORIZONTAL)
        self._default = kwargs.pop('default', '0')
        self._from_ = kwargs.pop('from', 1)
        self._to = kwargs.pop('to', 999)
        self._length = length=kwargs.pop('length', 250)
        self._width = kwargs.pop('width', 5)
        super().__init__(master, *args, **kwargs)
        self.label = ttk.Label(self, text=self._text)
        self.scale = ttk.Scale(self, orient=self._orient, value=self._default, from_=self._from_, to=self._to, length=length, command=self.UpdateEntry)
        self.entry = DefaultValueEntry(self, width=self._width, cursor='xterm', takefocus=False, default=self._default, func=self.UpdateScale, justify='center')
        self.enabled = True

    def UpdateEntry(self, value):
        self.entry.delete()
        self.entry.insert(str(int(float(value))))
        self.entry.default = str(int(float(value)))

    def UpdateScale(self):
        value = int(float(self.scale.get()))
        if value > 0 and value < 1000:
            self.scale.set(value)

    def Enable(self):
        if not self.enabled:
            self.label.configure(state=tkinter.NORMAL)
            self.scale.configure(state=tkinter.NORMAL)
            self.entry.reset()
            self.enabled = True

    def Disable(self):
        if self.enabled:
            self.label.configure(state=tkinter.DISABLED)
            self.scale.configure(state=tkinter.DISABLED)
            self.entry.delete()
            self.enabled = False

    def pack(self, *args, **kwargs):
        super().pack(pady=kwargs.pop('pady', 10), padx=kwargs.pop('padx', 10))
        self.label.pack()
        self.scale.pack()
        self.entry.pack()

    def get(self):
        return self.scale.get()