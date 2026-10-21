"""Owned fixture protocol regression; no sockets, credentials or device data."""
import unittest
from console_input import ConsoleInput


class ConsoleInputTests(unittest.TestCase):
    def test_cr_executes_and_bare_lf_does_not(self):
        console = ConsoleInput()
        self.assertEqual([], console.feed(b"show version\n"))
        console = ConsoleInput()
        self.assertEqual([b"show version"], console.feed(b"show version\r"))

    def test_crlf_is_one_command_at_every_packet_boundary(self):
        wire = b"terminal length 0\r\nshow version\r\n"
        for split in range(len(wire) + 1):
            console = ConsoleInput()
            with self.subTest(split=split):
                self.assertEqual([b"terminal length 0", b"show version"], console.feed(wire[:split]) + console.feed(wire[split:]))

    def test_fragmented_cr_and_coalesced_commands_do_not_create_blank_lines(self):
        console = ConsoleInput()
        lines = []
        for byte in b"show version\r\nshow inventory\rshow interfaces\r\n":
            lines += console.feed(bytes([byte]))
        self.assertEqual([b"show version", b"show inventory", b"show interfaces"], lines)

    def test_input_is_bounded(self):
        with self.assertRaises(ValueError):
            ConsoleInput(4).feed(b"longer")


if __name__ == "__main__":
    unittest.main()
