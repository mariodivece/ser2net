# (Uno) ser2net
A cross-platform service/daemon program that proxies raw serial port communications through TCP sockets. The idea was originally taken from the [ser2net](https://linux.die.net/man/8/ser2net) linux program, but confugration and implementation is entirely independent.

## The Problem

Since a few of us use Hyper-V to run or test [Home Assistant](https://www.home-assistant.io/), and Hyper-V is a [Type 1 hypervisor](https://learn.microsoft.com/en-us/windows-server/administration/performance-tuning/role/hyper-v-server/architecture), it proves very difficult to pass certain hardware that is essential to connect devices, such as the [Sonoff Zigbee USB Dongle](https://sonoff.tech/product/gateway-and-sensors/sonoff-zigbee-3-0-usb-dongle-plus-e/). Most of these USB dongles are interfaced via commands on a Serial/COM port, we can simply write a TCP server that will send and receive these commands over TCP sockets and that way, we can establish a connection to the host inside the VM, without the need to pass the USB hardware itself from the host to the VM. 

*Fig. 1 - Typical Application Diagram*
```
 ┌──────────┐                ┌────────────┐
 │          ├─┐   (COM3)     │            │
 │  Zigbee  │ │  ─────────►  │            │
 │  Dongle  ├─┘              │            │
 └──────────┘                │  Hyper-V   │
                             │    Host    │
 ┌─────────────┐             │            │
 │    HAOS     │             │            │
 │     VM      │             │            │
 ├─────────────┤  TCP Socket ├────────────┤
 │             │             │ unoser2net │
 │ Zigbee2MQTT │ ◄────────── │ TCP Server │
 │             │             │            │
 └─────────────┘             └────────────┘
```

## Installation and Use
  1. Go the the [Releases](https://github.com/mariodivece/ser2net/releases) page and find the latest one. Under **Assets**, download `unoser2net.exe` and `unoser2net.json`
  1. Place the 2 files under a directory you can remember easily. For example, `C\unopser2net\`
  1. Use your favorite plain-text editor to open the `unoser2net.json` file. Note that `Connections` is a json array, meaning you can proxy multiple devices/dongles/COM ports, but we will use only 1 entry. Fill it in as follows:
      1. `ServerIP`: Leave an empty string to make the server run on all network addresses, or fill in the IP address that you want to host your connection on.
      1. `ServerPort`: For these Zigbee dongles, `20108` is typical, but change it if you want a server on a different port.
      1. `PortName`: This is the most important setting. When you connect your dongle, you should see under `Ports (COM & LPT)`, that your device name appears with a COM designation in parenthesis -- for example: `Silicon Labs (COM3)`. In this case you would have to use `COM3` as the value for this setting.
      1. `BaudRate`, `DataBits`, `Parity`, `StopBits`. These are typically `115200-8-N-1` but your dongle might need different serial port settings.
  1. Once your settings seem correct, you can either run `unoser2net.exe` in a terminal/console window to watch the output and test your settings. It will also create some log file in the folder the program is running.
  1. If your settings are correct, stop the program with `Ctrl+C` and proceed to install the program as a WIndows service: `unoser2net.exe --install`. Log files will be created but there will be no console output. You can always remove the service by running `unoser2net.exe --remove`.

### Connecting Zigbee2MQTT to your unoser2net service.

Follow setp 3 in [this guide](https://www.zigbee2mqtt.io/advanced/remote-adapter/connect_to_a_remote_adapter.html). That is, edit your Zigbee2MQTT `configuration.yaml` file. Where you need to replace `192.168.2.13` with the actual IP of your Hyper-V host, and the port is what you previosuly set in your `PortNumber`.

```yaml
serial:
    port: 'tcp://192.168.2.13:20108'
```

Make sure you start/restart the Zigbee2MQTT add-on/service.

## Important Notices

  - As of right now, there is no plan to support SSL or [RFC2217](https://datatracker.ietf.org/doc/html/rfc2217).
  - Feel free to fork or copy my code. This was made in my spare time, as a hobby, and in the spirit of sharing.
  - Linux systemd installation support is still pending as of 5/17/2024. You should, haowever, be able to compile and run this program, and manually create and enable a systemd unit file.
  - The code is fairly well-written but it could use some improvements. You will find that some bits are over-engineered but that was just me having some fun.
  - This program comes with no warranties, free of charge, and I can't be held liable for any damages or side effects it may cause. Use at your own risk.
  - If you find issues, please open a new issue. There are no strict rules here. Just common sense, such as letting me know with reasonable amount of detail how to reproduce bugs.
  - Feel free to submit pull requests. All contributions are welcome!
  - If you have the time and skills, and would like to commit to maintaining this code long term, I can add a notice and a link to your repo, and make this one read-only.

That's it. Good luck and have fun!
*-- MAD --*