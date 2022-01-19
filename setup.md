# Tidal Clock Setup

## Materials

* [Raspberry Pi 4 2GB ($46)](https://www.amazon.com/Raspberry-Model-2019-Quad-Bluetooth/dp/B07TD42S27)
* [10 inch screen ($140)](https://www.amazon.com/Raspberry-Screen-10-1-IPS-SunFounder/dp/B07FZZ95WN)
* [Tidal Clock (software)](https://github.com/speedyjeff/tides)

![front](https://github.com/speedyjeff/tides/blob/master/media/front.png) 
![back](https://github.com/speedyjeff/tides/blob/master/media/back.png) 

### Optional

* [AcuRite 5-in-1 02064 Weather Station ($155)](https://www.amazon.com/AcuRite-Station-Weather-Ticker-Forecast/dp/B0147DCLPC)
* [Raspberry Pi 4 2GB ($46)](https://www.amazon.com/Raspberry-Model-2019-Quad-Bluetooth/dp/B07TD42S27)
* [Raspberry Pi 4 case ($9)](https://www.amazon.com/MazerPi-Raspberry-Cooling-Heatsink-Model/dp/B07W3ZMVP1)

![station](https://github.com/speedyjeff/tides/blob/master/media/station.jpg) 

## Setup

### One Time Setup

* [Ubuntu Server 32bit](https://ubuntu.com/tutorials/how-to-install-ubuntu-on-your-raspberry-pi)
  * choose lubuntu-desktop
  * (hint) wifi - /etc/netplan/50-cloud-init.yaml, quote both the network name and password

* Enable firewall
```
sh> sudo ufw allow ssh
sh> sudo ufw allow ftp
sh> sudo ufw enable
```

* [Enable ftp](https://www.osradar.com/how-to-set-up-an-ftp-server-ubuntu-20-04)
```
sh> sudo systemctl restart vsftpd
sh> sudo systemctl status vsftpd
```

* Create ftp user

### Tidal Clock

#### One Time Setup
* (one time) Setup Tides Directory and auto launch
```
sh> sudo mkdir /home/tides
sh> sudo chown -R <user name> tides
sh> vi /home/tides/launch.sh
  cd /home/tides
  dt=$(date)
  echo $dt $HOSTNAME > log
  unclutter -idle 1 -root &
  ./directoryserver -port 8000 -dir wwwroot/ -noshutdown >> log &
  sleep 5
  ps -aux | grep -i server >> log
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

* (ont time) Install Firefox Add-ons
  * [CORS Everywhere](https://addons.mozilla.org/en-US/firefox/addon/cors-everywhere)
    * Preferences - Enabled on Startup 'On'
  * [ForceFull](https://addons.mozilla.org/en-US/firefox/addon/forcefull)

* (one time)Install unclutter to hide mouse
```
sh> sudo apt install unclutter
sh> unclutter -idle 1 -root &
```

* Settings -> Power 
  * Automatic suspend - disabled
  * Power Button Action - power off
  * Blank screen - never
* Settings -> Notifications -> Do not distrub
  * Printer - no notifications

#### Update

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

#### Run tidal clock
```
sh> cd /home/tides
sh> ./launch.sh
OR
sh> ./directoryserver -port 8000 -dir wwwroot/ -noshutdown &
sh> firefox http://127.0.0.1:8000 &
```

### Weather Station Hub

#### One Time Setup

* Allow access to tcp port 11000
```
sh> sudo ufw allow 11000/tcp
```

* [change host name](https://www.howtogeek.com/197934/how-to-change-your-hostname-computer-name-on-ubuntu-linux)
```
sh> vi /etc/hostname
acuritehub
```
* (one time) Setup Acurite Hub Directory and auto launch
```
sh> mkdir /home/acuritehub
sh> sudo chown -R <user name> acuritehub
```

* Setup the code to run on boot
```
sh> sudo vi /etc/systemd/system/acuritehub.service
[Unit]
Description=AcuriteHub
After=network-online.target
Wants=network-online.target

[Service]
User=root
ExecStart=/home/acuritehub/acuritehub
Restart=always

[Install]
WantedBy=multi-user.target

sh> systemctl daemon-reload
sh> systemctl enable acuritehub
sh> systemctl start acuritehub
sh> systemctl status acuritehub
```

#### Update

* [AcuriteHub](https://github.com/speedyjeff/tides/tree/master/src/acuritehub)
  * Publish as 'Release + linux-arm32 + self-contained + trimmed + single file'
```
ftp> put <local dir>acuritehub\bin\Release\net5.0\publish\acuritehub acuritehub
sh> cd /home/acuritehub
sh> systemctl stop acuritehub
sh> cp /home/<ftp user>/acuritehub /home/acuritehub/acuritehub
sh> chmod +x acuritehub
```

#### Run Acurite Hub
```
sh> systemctl start acuritehub
```
