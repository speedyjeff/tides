# Tidal Clock Setup

## Materials

* [Raspberry Pi 4 2GB ($46)](https://www.amazon.com/Raspberry-Model-2019-Quad-Bluetooth/dp/B07TD42S27)
* [10 inch screen ($140)](https://www.amazon.com/Raspberry-Screen-10-1-IPS-SunFounder/dp/B07FZZ95WN)
* [Tidal Clock (software)](https://github.com/speedyjeff/tides)

![front](https://github.com/speedyjeff/tides/blob/master/media/front.png) 
![back](https://github.com/speedyjeff/tides/blob/master/media/back.png) 

## Setup

### One Time Setup

* [Ubuntu Server 32bit](https://ubuntu.com/tutorials/how-to-install-ubuntu-on-your-raspberry-pi)
  * choose lubuntu-desktop
  * (hint) wifi - /etc/netplan/50-cloud-init.yaml

* Enable firewall
```
sh> sudo ufw enable
sh> sudo ufw allow ssh
sh> sudo ufw allow ftp
```

* [Enable ftp](https://www.osradar.com/how-to-set-up-an-ftp-server-ubuntu-20-04)
```
sh> sudo systemctl restart vsftpd
sh> sudo systemctl status vsftpd
```

* Install unclutter to hide mouse
```
sh> sudo apt install unclutter
sh> unclutter -idle 1 -root &
```

* Setup Tides Directory and auto launch
```
sh> mkdir /home/tides
sh> vi /home/tides/launch.sh
  cd /home/tides
  unclutter -idle 1 -root &
  ./directoryserver -port 8000 -dir wwwroot/ -noshutdown &
  sleep 5
  firefox http://127.0.0.1:8000 &
sh> chmod +x /home/tides/launch.sh
sh> mkdir ~/.config/autostart
sh> vi ~/.config/autostart/.desktop
  [Desktop Entry]
  Type=Application
  Name=Tidal Clock
  Exec=/home/tides/launch.sh
  X-GNOME-Autostart-enabled=true
```
* Install Firefox Add-ons
  * [CORS Everywhere](https://addons.mozilla.org/en-US/firefox/addon/cors-everywhere)
    * Preferences - Enabled on Startup 'On'
  * [ForceFull](https://addons.mozilla.org/en-US/firefox/addon/forcefull)

* Create ftp user
* Settings -> Power 
  * Automatic suspend - when idle 1 hour
  * Power Button Action - power off
  * Blank screen = never
* Settings -> Notifications -> Do not distrub
  * Printer - no notifications

### Update Tidal Clock

* [Http file server](https://github.com/speedyjeff/directoryserver)
  * Publish as 'Release + linux-arm32 + self-contained + trimmed + single file'
```
ftp> put <local dir>\directoryserver\directoryserver\bin\Release\net5.0\publish\directoryserver directoryserver
sh> cd /home/tides
sh> cp /home/<ftp user>/directoryserver /home/tides/directoryserver
sh> chmod +x directoryserver
```

* [Tidal Clock](https://github.com/speedyjeff/tides)
  * Publish as 'Release + browser-wasm'
```
cmd> cd src\tidalclock\bin\Release\net5.0\browser-wasm\publish
cmd> tar -cf tidalclock.tar .
ftp> put <local dir>\src\tidalclock\bin\Release\net5.0\browser-wasm\publish\tidalclock.tar tidalclock.tar
sh> cd /home/tides
sh> sudo tar -xf ../<ftp user>/tidalclock.tar .
```

* Run tidal clock
```
sh> cd /home/tides
sh> ./launch.sh
OR
sh> ./directoryserver -port 8000 -dir wwwroot/ -noshutdown &
sh> firefox http://127.0.0.1:8000 &
```
