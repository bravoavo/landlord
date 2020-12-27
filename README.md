![image](https://github.com/bravoavo/LandLord/blob/main/rust-landlord.png?raw=true)
Land Lord is a modification for the original game. 

## Game Process

**Goal:**
1. Take control of the entire map cell by cell

**Rules:**
1. To capture a map cell build "Large banner on Pole"
2. Each captured map cell increases your gather rate for 3%
3. If someone destroys your "Large banner on Pole" gather rate decreased
4. Cells owners shows on the game map
5. The one cell the one "Large banner on Pole"

## Chat Commands

* `/lord` - Show your statistics
* `/lordclr` - Leave team

## Install, Backup and Re-Install
The plugin works with Zona Manager! Initialization has two simple steps:
- Getting the chosen map size and generating a bunch of zones (each zone equals the game map cell)
- Initialize Landlord by creating empty Landlord.data config file under ../oxide/data directory

The first initialization takes time up to 30 seconds. It depends on your map size.

All sensitive data stored at Landlord.data file. All data tied to ZoneManager data and the game map siz.e

Backup-restore functional is not emplimeted yet, but you can try: 
- To backup all flags save Landlord.data config file
- To clear all flags just delete Landlord.data config file
Your backup will work only with your server backup. You can't bring up a new server instance and copy Landlord.data there.

We working on a full-working restore tool.

Feel free to share ideas and send merge requests.
