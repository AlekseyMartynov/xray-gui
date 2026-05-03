CODENAME=$(lsb_release -cs)
DOTNET_VER=9.0.313

wget -nv https://dl.winehq.org/wine-builds/winehq.key -O - | sudo gpg --dearmor -o /etc/apt/keyrings/winehq-archive.key
sudo wget -nv https://dl.winehq.org/wine-builds/ubuntu/dists/${CODENAME}/winehq-${CODENAME}.sources -P /etc/apt/sources.list.d/
sudo dpkg --add-architecture i386
sudo apt-get update
sudo apt-get install -qq winehq-stable

mkdir ~/dotnet-win
cd    ~/dotnet-win
wget -nv https://builds.dotnet.microsoft.com/dotnet/Sdk/${DOTNET_VER}/dotnet-sdk-${DOTNET_VER}-win-x64.zip -O dotnet.zip
unzip -q dotnet.zip
