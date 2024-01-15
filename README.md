# ser2net
A TCP server that wraps serial prot communications

This is a sample project that shows has to configure a Hosted Service to act as a Windows Service, Systemd, or console app.
Allows the user to set a serial port and its parameters, creates a raw TCP server and finally allows a TCP connection for communicating with the serial port.
As of right now there is no support for SSL or [RFC2217](https://datatracker.ietf.org/doc/html/rfc2217).
