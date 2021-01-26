import threading

class STWThread(threading.Thread):
    """
    Stoppable Tracked Waiting Thread.
    A wrapper for python's threading.Thread class to create functionality for a thread
    which waits for certain conditions to be met before executing its task. If the assinged
    exit flags are set whilst waiting, the thread will stop waiting and can die. Threads are
    trakced by adding them to the global thread Queue object. All wait flags must be set for
    the task to be performed, but only one quit flag needs to be set to allow the thread to die.
    All STWThreads are assigned the global QUIT event as an exitFlag
    """
    def __init__(self, *args, **kwargs):
        self.mainFunction = kwargs.pop('mainFunction', lambda: None)
        self.waitFlags = []
        if 'waitFlags' in kwargs.keys():
            waitFlags = kwargs.pop('waitFlags')
            if isinstance(waitFlags, list):
                self.waitFlags += waitFlags
            else:
                self.waitFlags.append(waitFlags)
        self.exitFlags = [QUIT]
        if 'exitFlags' in kwargs.keys():
            exitFlags = kwargs.pop('exitFlags')
            if isinstance(exitFlags, list):
                self.exitFlags += exitFlags
            else:
                self.exitFlags.append(exitFlags)
        self.timeout = kwargs.pop('timeout', 5)
        super().__init__(target=self.JobLoop, *args, **kwargs)

    def JobLoop(self):
        threads.Add(self)
        waitFlag = None
        breakWhile = False
        waited = False
        alreadyIn = False
        while True:
            for flag in self.waitFlags:
                if not flag.is_set():
                    waitFlag = flag
                    break
                waited = True
            if not waited:
                if waitFlag.wait(timeout=self.timeout):
                    if waited and not alreadyIn:
                        alreadyIn = True
                        self.mainFunction()
                        break
                for exitFlag in self.exitFlags:
                    if exitFlag.is_set():
                        breakWhile = True
                        break
                if breakWhile:
                    break
            else:
                self.mainFunction()
                break
        threads.Remove(self)

class Queue(list):
    """
    A wrapper for python's list class to implement a basic, thread safe FIFO data structure
    """
    def __init__(self):
        self.lock = threading.Lock()
        self.empty = threading.Event()
        super().__init__()

    def Add(self, item):
        self.lock.acquire()
        self.append(item)
        self.empty.clear()
        self.lock.release()

    def Get(self):
        self.lock.acquire()
        item = self.pop(0)
        self.lock.release()
        if len(self) == 0:
            self.empty.set()
        return item

    def Remove(self, item):
        self.lock.acquire()
        self.remove(item)
        self.lock.release()
        if len(self) == 0:
            self.empty.set()

    def Empty(self):
        return self.empty._flag

QUIT = threading.Event()
threads = Queue()