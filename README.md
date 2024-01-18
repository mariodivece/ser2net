# unoser2net
A cross-platform service/daemon app that proxies raw serial port communications through TCP sockets.

## Problem
I wanted to run Zigbee2MQTT as a Hyper-V VM. Since Hyper-V is a Type-1 hypervisor, there is no way to forward a Zigbee USB stick to Zigbee2MQTT directly, and instead, the connection to the Zigbee stick has to be made through the network. So, I needed to just plug in the stick to the Hyper-V host, forward seria port communiocations back and forth over a TCP socket and I found it extremely hard to find a Windows program that I could install as a service and perform this simple task.

## Solution
I made this as my small contribution to the Zigbee2MQTT project and really just to practice some patterns and expriment with new ideas. All contributions are of course welcome!


Note: As of right now there is no plan to support SSL or [RFC2217](https://datatracker.ietf.org/doc/html/rfc2217).
