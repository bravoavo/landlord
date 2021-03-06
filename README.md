![image](https://github.com/bravoavo/LandLord/blob/main/rust-landlord.png?raw=true)
Land Lord is a modification for the original game. 

## Game Process

**Goal:**
1. Take control of the entire map cell by cell

**Rules:**
1. To capture a map cell build "Large banner on Pole"
2. Each captured map cell increases your gather rate for 3% by default
3. If someone destroys your "Large banner on Pole" gather rate decreased
4. Cells owners shows on the game map
5. The one cell the one "Large banner on Pole"

## Clans plugin support

The plugin supports the Clans plugin. Clan members share gather same rate and same clan flag. If a member captures a cell it will increase the clan gater rate.  
The clan flag is a clan owner flag, so if the owner leaves the clan, he takes all flags with him. (!) CAUTION Change a clan leader can lead to unstable work.


## Permissions

landlord.admin -- Allows player to use the chat commands starts with /lordadmin 

* `/lordadmin notrespass enable|disable` - Get gather bonus only in not captured cells or cells you own. Enabled by default.
* `/lordadmin onlyconnected enable|disable` - Allow to capture only connected cells. Enabled by default.
* `/lordadmin graycircles enable|disable` - Show gray circles on captured cells. Enabled by default.
* `/lordadmin gatherratio [from 1 to 1000]` - Set gather ratio in percentage. Default 3.
* `/lordadmin settings` - Show current settings of notrespass and onlyconnected modes.
	
## Chat Commands

* `/lord` - Show your statistics
* `/lordclr` - Leave team

## Installation
The plugin works with Zona Manager! Initialization has two simple steps:
- Getting the chosen map size and generating a bunch of zones (each zone equals the game map cell)
- Initialize Landlord by creating empty Landlord.data config file under ../oxide/data directory

The first initialization takes time up to 30 seconds. It depends on your map size.
All sensitive data stored at `Landlord.data` file. All data tied to ZoneManager data and the game map size

Feel free to share ideas and send merge requests.