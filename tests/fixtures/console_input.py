"""Bounded owned-fixture console input: CR executes; optional following LF is consumed.

Do not accept bare LF as Enter: that previously made the SSH Collector tests pass
without exercising an interactive terminal's CR input contract.
"""


class ConsoleInput:
    def __init__(self, limit=2048):
        self.pending = bytearray()
        self.after_cr = False
        self.limit = limit

    def feed(self, data):
        lines = []
        for value in data:
            if self.after_cr:
                self.after_cr = False
                if value == 10:
                    continue
            if value == 13:
                lines.append(bytes(self.pending))
                self.pending.clear()
                self.after_cr = True
            else:
                self.pending.append(value)
                if len(self.pending) > self.limit:
                    raise ValueError("Owned fixture console input limit exceeded")
        return lines

    def clear_pending(self):
        self.pending.clear()
