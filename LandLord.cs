using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("LandLord", "bravoavo", "1.0.17")]
    [Description("Take control of the map")]
    class LandLord : RustPlugin
    {
        [PluginReference] Plugin ZoneManager, Clans;

        ConfigData configData;
        private DynamicConfigFile data;

        //Constant initialization
        readonly uint bannerprefabid = 3188315846;
        readonly float size = 146.33f;
        readonly bool debug = true;
        int notrespassgather = 1;
        int onlyconnected = 1;
        int gatherratio = 3;
        int graycircles = 1;
        readonly string zonenameprefix = "LandlordZone";
        readonly string datafile_name = "Landlord.data";
        private const string permUse = "landlord.admin";
        readonly Color[] colorArray = new Color[] { Color.black, Color.white, Color.red, Color.blue, Color.green, Color.yellow, Color.cyan, Color.gray, new Color32(255, 157, 0, 1) };
        private Color colors;
        public Dictionary<string, int> currentsettings = new Dictionary<string, int>();
        public Dictionary<ulong, HashSet<int[]>> flagi = new Dictionary<ulong, HashSet<int[]>>();
        public Dictionary<ulong, List<string>> quadrants = new Dictionary<ulong, List<string>>();
        public Dictionary<string, Vector3> poles = new Dictionary<string, Vector3>();
        public Dictionary<ulong, int> gatherMultiplier = new Dictionary<ulong, int>();
        public Dictionary<string, ulong> teamList = new Dictionary<string, ulong>();

        public Dictionary<string, MapMarkerGenericRadius> allmarkers = new Dictionary<string, MapMarkerGenericRadius>();
        public Dictionary<string, List<MapMarkerGenericRadius>> flagmarkers = new Dictionary<string, List<MapMarkerGenericRadius>>();

        public List<string> blockedItemsList { get; set; } = new List<string>();

        #region Localization
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LordStatus"] = "<color=orange>LANDLORD:</color> Grounds seized: {0} TeamID is: {1}",
                ["UsageAdmin"] = "<color=orange>LANDLORD:</color> Usage:\n /lordadmin notrespass enable|disable \n " +
                "/lordadmin onlyconnected enable|disable " +
                "\n /lordadmin graycircles enable|disable " +
                "\n /lordadmin gatherratio [from 1 to 1000] " +
                "\n /lordadmin settings " +
                "\n /lordadmin blockitem [item] \n /lordadmin unblockitem [item] \n /lordadmin blockitemlist \n /lordadmin blockitemclear",
                ["NoPermission"] = "<color=orange>LANDLORD:</color> You do not have the permissions to use this command.",
                ["LordStatusZero"] = "<color=orange>LANDLORD:</color> Grounds seized: 0 TeamID is: {0}",
                ["TeamClearSuccess"] = "<color=orange>LANDLORD:</color> Team clear: Success!",
                ["BlockedItemsEditSuccess"] = "<color=orange>LANDLORD:</color> Blocked items list edit: Success!",
                ["BlockedItemsClearSuccess"] = "<color=orange>LANDLORD:</color> Blocked items clear: Success!",
                ["BlockedItems"] = "<color=orange>LANDLORD:</color> Blocked Items: {0}",
                ["HaveNoTeam"] = "<color=orange>LANDLORD:</color> You are have no team!",
                ["CellHasPole"] = "<color=orange>LANDLORD:</color> This map cell already has a pole! Destroy it before build a new one",
                ["SettingsChanged"] = "<color=orange>LANDLORD:</color> Settings changed!",
                ["CurrentSettings"] = "<color=orange>LANDLORD:</color> notrespass {0} \n onlyconnected {1} \n graycircles {2} \n gatherratio {3}",
                ["NotrespassStatus"] = "<color=orange>LANDLORD:</color> notrespass mode is {0} to switch use /lordadmin notrespass enable|disable ",
                ["OnlyconnectedStatus"] = "<color=orange>LANDLORD:</color> onlyconected mode is {0}",
                ["GatherratioStatus"] = "<color=orange>LANDLORD:</color> Gatherratio set to {0}",
                ["GraycirclesStatus"] = "<color=orange>LANDLORD:</color> graycircles is {0}",
                ["CellNotConnected"] = "<color=orange>LANDLORD:</color> The cell you try to capture not connected to another yours",
                ["CellCaptured"] = "<color=orange>LANDLORD:</color> You deploy pole! Your gather rate increased!",
                ["CellCapturedGlobal"] = "<color=orange>LANDLORD:</color> Player {0} has captured the cell {1}",
                ["CellFreedGlobal"] = "<color=orange>LANDLORD:</color> The cell {0} has freed up"
            }, this);
        }

        #endregion Localization

        #region ZoneInitialization
        private void MyZonesInit()
        {
            float mapsize = TerrainMeta.Size.x;
            int counter = 0;
            int j = 100;
            var celltogenerate = Math.Pow(Math.Round(mapsize / size), 2);
            Puts("Generating the cell map. Nubmer to generate is " + celltogenerate);
            for (var x = -mapsize / 2 + size / 2; x <= mapsize / 2; x = x + size)
            {
                int i = 100;
                for (var z = -mapsize / 2 + size / 2; z <= mapsize / 2; z = z + size)
                {
                    string[] array = new string[] { "name", zonenameprefix + j + i, "size", "146.3 500 146.3", "location", x + " 0 " + z };
                    ZoneManager.Call("EraseZone", j + i.ToString().PadLeft(3, '0'));
                    ZoneManager.Call("CreateOrUpdateZone", j + i.ToString().PadLeft(3, '0'), array);
                    i++;
                    counter++;
                }
                j++;
                Puts("Generating...> " + counter + " of " + celltogenerate);
            }
            Puts("Generating is done!");
            var InitFile = Interface.Oxide.DataFileSystem.GetFile(datafile_name);

            InitFile.Save();

        }

        #endregion

        #region InitPlugin
        private void OnServerInitialized()
        {
            permission.RegisterPermission(permUse, this);
            if (Clans == null || !Clans.IsLoaded)
            {
                PrintError("Missing plugin dependency Clans: https://umod.org/plugins/clans");
                return;
            }
            if (ZoneManager != null && ZoneManager.IsLoaded)
            {
                if (!Interface.Oxide.DataFileSystem.ExistsDatafile(datafile_name))
                {
                    MyZonesInit();
                }
            }
            else
            {
                PrintError("Missing plugin dependency Zone Manager: https://umod.org/plugins/zone-manager");
                return;
            }
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(datafile_name))
            {
                data = Interface.Oxide.DataFileSystem.GetDatafile(datafile_name);
            }
            LoadData();
        }

        #endregion

        #region PlayerInit

        void OnPlayerSleepEnded(BasePlayer player)
        {
            foreach (var quadrat in quadrants)
            {
                foreach (var zone in quadrat.Value)
                {
                    if (currentsettings["graycircles"] == 1) allmarkers[zone].SendUpdate();
                    flagmarkers[zone][0].SendUpdate();
                    flagmarkers[zone][1].SendUpdate();
                    flagmarkers[zone][2].SendUpdate();

                }
            }
            if (!flagi.ContainsKey(player.userID)) GenarateFlag(player.userID);
        }

        private void GenarateFlag(ulong playerId)
        {
            if (flagi.Count < 900)
            {
                System.Random rnd = new System.Random();
                int[] colorsArrayTmp;
                flagi.Add(playerId, new HashSet<int[]>());
                do
                {
                    int color1 = rnd.Next(0, 8);
                    int color2 = rnd.Next(0, 8);
                    int color3 = rnd.Next(0, 8);
                    colorsArrayTmp = new int[] { color1, color2, color3 };
                } while (!flagi[playerId].Add(colorsArrayTmp));
            }
        }

        #endregion

        #region OxideHooks

        void OnEntityKill(BaseEntity entity)
        {
            if (entity.prefabID == bannerprefabid)
            {
                if (debug) Puts("Entity 'Banner on Pole' killed - Prefab ID is " + entity.prefabID.ToString() + " Location " + entity.ServerPosition);

                var zoneid = poles.FirstOrDefault(x => x.Value == entity.ServerPosition).Key;
                if (zoneid != null)
                {
                    if (debug) Puts("ZoneID id " + zoneid);
                    if (currentsettings["graycircles"] == 1)
                    {
                        DeleteMarkerFromMap(allmarkers[zoneid]);
                        allmarkers.Remove(zoneid);
                    }
                    DeleteMarkerFromMap(flagmarkers[zoneid][0]);
                    DeleteMarkerFromMap(flagmarkers[zoneid][1]);
                    DeleteMarkerFromMap(flagmarkers[zoneid][2]);
                    flagmarkers.Remove(zoneid);
                    ulong teampid = 0;
                    foreach (KeyValuePair<ulong, List<string>> playerid in quadrants)
                    {
                        var match = playerid.Value.FirstOrDefault(stringToCheck => stringToCheck.Contains(zoneid));
                        if (match != null)
                        {
                            teampid = playerid.Key;
                            break;
                        }
                    }
                    if (teampid == 0)
                    {
                        if (debug) Puts("Can't found cell owner");
                        return;
                    }
                    quadrants[teampid].Remove(zoneid);
                    if (quadrants[teampid].Count == 0) quadrants.Remove(teampid);
                    poles.Remove(zoneid);
                    if (gatherMultiplier.ContainsKey(teampid))
                    {
                        if (gatherMultiplier[teampid] > 0) gatherMultiplier[teampid]--;
                    }
                    PrintToChat(Lang("CellFreedGlobal", "1", GetGrid(entity.ServerPosition)));
                    SaveData();
                }
                else if (debug) Puts("Zone id not found");
            }
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            BasePlayer player = plan.GetOwnerPlayer();
            ulong teampid;
            string playerClan = Clans.Call<string>("GetClanOf", player);
            if (playerClan != null && teamList.ContainsKey(playerClan)) teampid = teamList[playerClan];
            else teampid = player.userID;
            string[] zids = (string[])ZoneManager.Call("GetPlayerZoneIDs", player);
            var entity = go.ToBaseEntity();
            if (entity.prefabID == bannerprefabid && zids.Length > 0)
            {
                string curentZoneId = GetLordZoneId(zids);
                if (debug) Puts("Banner entity was built. Prefab ID is " + entity.prefabID + " zone ID is " + curentZoneId);
                if (!quadrants.ContainsKey(teampid))
                {
                    quadrants.Add(teampid, new List<string>());
                    quadrants[teampid].Add(curentZoneId);
                }
                else
                {
                    var match = quadrants[teampid].FirstOrDefault(stringToCheck => stringToCheck.Contains(curentZoneId));
                    if (match != null) return;
                    else
                    {
                        quadrants[teampid].Add(curentZoneId);
                    }
                }
                if (!gatherMultiplier.ContainsKey(teampid))
                {
                    gatherMultiplier.Add(teampid, 1);
                }
                else
                {
                    gatherMultiplier[teampid]++;
                }
                poles.Add(curentZoneId, entity.ServerPosition);
                if (debug) Puts("Server position " + entity.ServerPosition.ToString());
                var zonelocation = (Vector3)ZoneManager.Call("GetZoneLocation", curentZoneId);
                if (currentsettings["graycircles"] == 1) SpawnMarkerOnMap(zonelocation, curentZoneId);
                SpawnFlag(zonelocation, curentZoneId, teampid);
                player.ChatMessage(Lang("CellCaptured", player.UserIDString));
                PrintToChat(Lang("CellCapturedGlobal", player.UserIDString, player.displayName, GetGrid(entity.ServerPosition)));
                SaveData();
            }
            else if (debug) Puts("After build - Not found zone ID or prefab!");
        }

        void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            ulong teampid;
            string playerClan = Clans.Call<string>("GetClanOf", player);
            if (playerClan != null && teamList.ContainsKey(playerClan)) teampid = teamList[playerClan];
            else teampid = player.userID;
            // Check if the player has at least a captured cell and if not skip further processing 
            if (!quadrants.ContainsKey(teampid)) return;
            string[] zids = (string[])ZoneManager.Call("GetPlayerZoneIDs", player);
            string match = null;

            foreach (ItemAmount item in collectible.itemList)
            {
                if (blockedItemsList.Contains(item.itemDef.shortname)) return;

                if (currentsettings["notrespassgather"] == 1)
                {
                    if (zids.Length > 0)
                    {
                        if (debug) Puts("Looking for exact landlord zone");
                        string curentZoneId = GetLordZoneId(zids);
                        if (curentZoneId == "None")
                        {
                            if (debug) Puts("Landlord zone not found!");
                            return;
                        }
                        match = quadrants[teampid].FirstOrDefault(stringToCheck => stringToCheck.Contains(curentZoneId));
                        if (!poles.ContainsKey(curentZoneId) || (poles.ContainsKey(curentZoneId) && match != null))
                        {
                            if (gatherMultiplier.ContainsKey(teampid))
                            {
                                if (gatherMultiplier[teampid] > 0)
                                {
                                    float multiplier = (float)gatherMultiplier[teampid] * currentsettings["gatherratio"] / 100;
                                    item.amount = (int)(item.amount + 1 + item.amount * multiplier);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (gatherMultiplier.ContainsKey(teampid))
                    {
                        if (gatherMultiplier[teampid] > 0)
                        {
                            float multiplier = (float)gatherMultiplier[teampid] * currentsettings["gatherratio"] / 100;
                            item.amount = (int)(item.amount + 1 + item.amount * multiplier);
                        }
                    }
                }
            }
        }

        object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            ulong teampid;
            if (blockedItemsList.Contains(item.info.shortname)) return null;

            string[] zids = (string[])ZoneManager.Call("GetPlayerZoneIDs", player);
            string match = null;
            string playerClan = Clans.Call<string>("GetClanOf", player);
            if (playerClan != null && teamList.ContainsKey(playerClan)) teampid = teamList[playerClan];
            else teampid = player.userID;
            // Check if the player has at least a captured cell and if not skip further processing 
            if (!quadrants.ContainsKey(teampid)) return null;
            if (debug) Puts("Dispenser. Start amount is " + item.amount);
            if (currentsettings["notrespassgather"] == 1)
            {
                if (zids.Length > 0)
                {
                    if (debug) Puts("Looking for exact landlord zone");
                    string curentZoneId = GetLordZoneId(zids);
                    if (curentZoneId == "None")
                    {
                        if (debug) Puts("Landlord zone not found!");
                        return null;
                    }

                    match = quadrants[teampid].FirstOrDefault(stringToCheck => stringToCheck.Contains(curentZoneId));

                    if (!poles.ContainsKey(curentZoneId) || (poles.ContainsKey(curentZoneId) && match != null))
                    {
                        if (gatherMultiplier.ContainsKey(teampid))
                        {
                            if (gatherMultiplier[teampid] > 0)
                            {
                                float multiplier = (float)gatherMultiplier[teampid] * currentsettings["gatherratio"] / 100;
                                if (debug) Puts("Multiplier is " + multiplier);
                                item.amount = (int)(item.amount + 5 + item.amount * multiplier);
                            }
                        }
                    }
                }

            }
            else
            {
                if (gatherMultiplier.ContainsKey(teampid))
                {
                    if (gatherMultiplier[teampid] > 0)
                    {
                        float multiplier = (float)gatherMultiplier[teampid] * currentsettings["gatherratio"] / 100;
                        if (debug) Puts("Multiplier is " + multiplier);
                        item.amount = (int)(item.amount + 5 + item.amount * multiplier);
                    }
                }
            }

            return null;
        }

        object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            BasePlayer player = planner.GetOwnerPlayer();
            ulong teampid;
            string playerClan = Clans.Call<string>("GetClanOf", player);
            if (playerClan != null && teamList.ContainsKey(playerClan)) teampid = teamList[playerClan];
            else teampid = player.userID;
            string[] zids = (string[])ZoneManager.Call("GetPlayerZoneIDs", player);
            if (prefab.prefabID == bannerprefabid && zids.Length > 0)
            {
                if (debug) Puts("Looking for exact landlord zone");
                string curentZoneId = GetLordZoneId(zids);
                if (curentZoneId == "None")
                {
                    if (debug) Puts("Error! Landlord zone not found!");
                    return false;
                }
                if (currentsettings["onlyconnected"] == 1 && quadrants.ContainsKey(teampid))
                {
                    if (!CheckConnected(curentZoneId, teampid))
                    {
                        player.ChatMessage(Lang("CellNotConnected", player.UserIDString));
                        if (debug) Puts("Zone not connected to another!");
                        return false;
                    }
                }

                if (poles.ContainsKey(curentZoneId))
                {
                    player.ChatMessage(Lang("CellHasPole", player.UserIDString));
                    if (debug) Puts("Zone has a pole already!");
                    return false;
                }
                if (debug) Puts("Banner building allowed. Prefab ID is " + prefab.prefabID + " zone ID is " + curentZoneId);
                return null;
            }
            else if (debug) Puts("Before build - Not found zone ID or banner prefab.");
            return null;
        }

        private void OnClanCreate(string tag)
        {
            timer.Once(1f, () =>
            {
                JObject clan = GetClan(tag);
                if (debug) Puts("Clan owner " + clan["owner"]);
                teamList.Add(tag, (ulong)clan["owner"]);
                SaveData();
            });
        }

        private void OnClanDestroy(string tag)
        {
            if (debug) Puts("Existing clan destroyed");
            teamList.Remove(tag);
            SaveData();
        }

        #endregion

        #region MarkersAndMap

        private void DeleteMarkerFromMap(MapMarkerGenericRadius marker)
        {
            marker.Kill();
            marker.SendUpdate();
        }

        private void SpawnMarkerOnMap(Vector3 position, string curentZoneId)
        {
            int mapsize = (int)TerrainMeta.Size.x;
            colors = Color.black;
            float zoneradius = (100000f / mapsize) * 0.02f;
            float markeralpha = 0.5f;
            var mapMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position) as MapMarkerGenericRadius;
            mapMarker.alpha = markeralpha;
            mapMarker.color1 = colors;
            mapMarker.color2 = colors;
            mapMarker.radius = (float)Math.Round(zoneradius, 2);
            mapMarker.Spawn();
            mapMarker.SendUpdate();

            if (!allmarkers.ContainsKey(curentZoneId))
            {
                allmarkers.Add(curentZoneId, mapMarker);
            }
            else
            {
                if (debug) Puts("Marker already in dictionary");
            }
        }

        private void SpawnFlag(Vector3 position, string curentZoneId, ulong playerId)
        {
            if (!flagi.ContainsKey(playerId)) GenarateFlag(playerId);
            HashSet<int[]> array = flagi[playerId];
            List<int> listik = new List<int>();
            foreach (var ids in array)
            {
                listik.Add(ids[0]);
                listik.Add(ids[1]);
                listik.Add(ids[2]);
            }
            float mapsize = TerrainMeta.Size.x;
            Color color1 = colorArray[listik[0]];
            Color color2 = colorArray[listik[1]];
            Color color3 = colorArray[listik[2]];
            Vector3 position1;
            Vector3 position2;
            Vector3 position3;
            float zoneradius = (100000f / mapsize) * 0.002f;
            float markeralpha = 0.7f;
            position1.x = position.x - 63; position1.y = position.y; position1.z = position.z - 63;
            position2.x = position.x - 48; position2.y = position.y; position2.z = position.z - 63;
            position3.x = position.x - 33; position3.y = position.y; position3.z = position.z - 63;
            var mapMarker1 = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position1) as MapMarkerGenericRadius;
            mapMarker1.alpha = markeralpha; mapMarker1.color1 = color1; mapMarker1.color2 = color1; mapMarker1.radius = (float)Math.Round(zoneradius, 2);
            var mapMarker2 = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position2) as MapMarkerGenericRadius;
            mapMarker2.alpha = markeralpha; mapMarker2.color1 = color2; mapMarker2.color2 = color2; mapMarker2.radius = (float)Math.Round(zoneradius, 2);
            var mapMarker3 = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position3) as MapMarkerGenericRadius;
            mapMarker3.alpha = markeralpha; mapMarker3.color1 = color3; mapMarker3.color2 = color3; mapMarker3.radius = (float)Math.Round(zoneradius, 2);

            mapMarker1.Spawn(); mapMarker1.SendUpdate();
            mapMarker2.Spawn(); mapMarker2.SendUpdate();
            mapMarker3.Spawn(); mapMarker3.SendUpdate();

            if (!flagmarkers.ContainsKey(curentZoneId))
            {
                flagmarkers.Add(curentZoneId, new List<MapMarkerGenericRadius>());
                flagmarkers[curentZoneId].Add(mapMarker1);
                flagmarkers[curentZoneId].Add(mapMarker2);
                flagmarkers[curentZoneId].Add(mapMarker3);
            }
            else
            {
                if (debug) Puts("Flag marker already in dictionary");
            }
        }

        #endregion

        #region DataManagement

        class ConfigData
        {
            public Dictionary<ulong, HashSet<int[]>> FlagiData = new Dictionary<ulong, HashSet<int[]>>();
            public Dictionary<ulong, List<string>> QuadrantsData = new Dictionary<ulong, List<string>>();
            public Dictionary<string, Vector3> PolesLocationData = new Dictionary<string, Vector3>();
            public Dictionary<ulong, int> GatherRateData = new Dictionary<ulong, int>();
            public Dictionary<string, ulong> TeamListData = new Dictionary<string, ulong>();
            public Dictionary<string, int> LandlordSettings = new Dictionary<string, int>();
            public List<string> BlockedItems = new List<string>();
        }

        private void LoadData()
        {
            quadrants.Clear();
            flagi.Clear();
            gatherMultiplier.Clear();
            teamList.Clear();
            poles.Clear();
            currentsettings.Clear();
            blockedItemsList.Clear();
            try
            {
                if (debug) Puts("A try to use existing configuration");
                configData = data.ReadObject<ConfigData>();
                quadrants = configData.QuadrantsData;
                blockedItemsList = configData.BlockedItems;
                flagi = configData.FlagiData;
                gatherMultiplier = configData.GatherRateData;
                teamList = configData.TeamListData;
                poles = configData.PolesLocationData;
                currentsettings = configData.LandlordSettings;
                if (quadrants.Count > 0)
                {
                    foreach (var marker in UnityEngine.Object.FindObjectsOfType<MapMarkerGenericRadius>())
                    {
                        DeleteMarkerFromMap(marker);
                    }
                    foreach (var quadrat in quadrants)
                    {
                        foreach (var zone in quadrat.Value)
                        {
                            var zonelocation = (Vector3)ZoneManager.Call("GetZoneLocation", zone);
                            if (currentsettings["graycircles"] == 1) SpawnMarkerOnMap(zonelocation, zone);
                            SpawnFlag(zonelocation, zone, quadrat.Key);
                        }
                    }

                }
                if (!currentsettings.ContainsKey("notrespassgather"))
                {
                    currentsettings["notrespassgather"] = notrespassgather;
                }
                if (!currentsettings.ContainsKey("onlyconnected"))
                {
                    currentsettings["onlyconnected"] = onlyconnected;
                }
                if (!currentsettings.ContainsKey("graycircles"))
                {
                    currentsettings["graycircles"] = graycircles;
                }
                if (!currentsettings.ContainsKey("gatherratio"))
                {
                    currentsettings["gatherratio"] = gatherratio;
                }
                if (debug) Puts("Existing configuration found and loaded");
            }
            catch
            {
                if (debug) Puts("Existing configuration not found. A new configuration creating.");
                configData = new ConfigData();
                quadrants = configData.QuadrantsData;
                flagi = configData.FlagiData;
                gatherMultiplier = configData.GatherRateData;
                teamList = configData.TeamListData;
                poles = configData.PolesLocationData;
                currentsettings = configData.LandlordSettings;
                currentsettings["notrespassgather"] = notrespassgather;
                currentsettings["onlyconnected"] = onlyconnected;
                currentsettings["graycircles"] = graycircles;
                currentsettings["gatherratio"] = gatherratio;
            }
            SaveData();
        }

        private void SaveData()
        {
            data = Interface.Oxide.DataFileSystem.GetDatafile(datafile_name);
            data.WriteObject(configData);
        }

        private JObject GetClan(string tag)
        {
            JObject clan = (JObject)Clans.Call("GetClan", tag);
            if (clan == null)
            {
                if (debug) Puts("Clan " + tag + " not found");
                return null;
            }
            return clan;
        }

        #endregion

        #region ChatCommands

        [ChatCommand("lord")]
        private void CmdChatLordStats(BasePlayer player, string command, string[] args)
        {
            ulong teampid;
            string playerClan = Clans.Call<string>("GetClanOf", player);
            if (playerClan != null && teamList.ContainsKey(playerClan)) teampid = teamList[playerClan];
            else teampid = player.userID;

            if (gatherMultiplier.ContainsKey(teampid) && gatherMultiplier[teampid] > 0)
            {
                player.ChatMessage(Lang("LordStatus", player.UserIDString, gatherMultiplier[teampid].ToString(), teampid.ToString()));
            }
            else player.ChatMessage(Lang("LordStatusZero", player.UserIDString, teampid.ToString()));
        }

        [ChatCommand("lordadmin")]
        void LordAdminCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permUse))
            {
                player.ChatMessage(Lang("NoPermission", player.UserIDString));
                return;
            }
            if (debug) Puts("Args lenght " + args.Length);
            string cstatus, tstatus, gcstatus;
            if (args.Length > 0)
            {
                switch (args[0])
                {
                    default:
                        player.ChatMessage(Lang("UsageAdmin", player.UserIDString));
                        break;
                    case "nt":
                    case "notrespass":
                        if (args.Length == 2)
                        {
                            if (args[1] == "enable") currentsettings["notrespassgather"] = 1;
                            if (args[1] == "disable") currentsettings["notrespassgather"] = 0;
                        }
                        tstatus = (currentsettings["notrespassgather"] == 1) ? "Enabled" : "Disabled";
                        player.ChatMessage(Lang("NotrespassStatus", player.UserIDString, tstatus));
                        SaveData();
                        break;
                    case "oc":
                    case "onlyconnected":
                        if (args.Length == 2)
                        {
                            if (args[1] == "enable") currentsettings["onlyconnected"] = 1;
                            if (args[1] == "disable") currentsettings["onlyconnected"] = 0;
                        }
                        cstatus = (currentsettings["onlyconnected"] == 1) ? "Enabled" : "Disabled";
                        player.ChatMessage(Lang("OnlyconnectedStatus", player.UserIDString, cstatus));
                        SaveData();
                        break;
                    case "gc":
                    case "graycircles":
                        if (args.Length == 2)
                        {
                            if (args[1] == "enable" && currentsettings["graycircles"] == 0)
                            {
                                foreach (var quadrat in quadrants)
                                {
                                    foreach (var zoneid in quadrat.Value)
                                    {
                                        var zonelocation = (Vector3)ZoneManager.Call("GetZoneLocation", zoneid);
                                        if (debug) Puts("Spawn marker" + zoneid);
                                        SpawnMarkerOnMap(zonelocation, zoneid);
                                    }
                                }
                                currentsettings["graycircles"] = 1;
                            }
                            if (args[1] == "disable" && currentsettings["graycircles"] == 1)
                            {
                                foreach (var quadrat in quadrants)
                                {
                                    foreach (var zoneid in quadrat.Value)
                                    {
                                        if (debug) Puts("Delete marker" + zoneid);
                                        DeleteMarkerFromMap(allmarkers[zoneid]);
                                        allmarkers.Remove(zoneid);
                                    }
                                }
                                currentsettings["graycircles"] = 0;
                            }
                        }
                        gcstatus = (currentsettings["graycircles"] == 1) ? "Enabled" : "Disabled";
                        player.ChatMessage(Lang("GraycirclesStatus", player.UserIDString, gcstatus));
                        SaveData();
                        break;
                    case "gr":
                    case "gatherratio":
                        if (args.Length == 2)
                        {
                            int grtmp = Convert.ToInt32(args[1]);
                            if (grtmp > 0 && grtmp < 1001)
                            {
                                currentsettings["gatherratio"] = grtmp;
                            }
                            else
                            {
                                player.ChatMessage(Lang("UsageAdmin", player.UserIDString));
                            }
                        }
                        player.ChatMessage(Lang("GatherratioStatus", player.UserIDString, args[1]));
                        SaveData();
                        break;
                    case "st":
                    case "settings":
                        player.ChatMessage(Lang("CurrentSettings", player.UserIDString, tstatus = (currentsettings["notrespassgather"] == 1) ? "Enabled" : "Disabled", cstatus = (currentsettings["onlyconnected"] == 1) ? "Enabled" : "Disabled", gcstatus = (currentsettings["graycircles"] == 1) ? "Enabled" : "Disabled", currentsettings["gatherratio"]));
                        SaveData();
                        break;
                    case "blk":
                    case "blockitem":
                        if (args.Length == 2)
                        {
                            string item = Convert.ToString(args[1]);
                            blockedItemsList.Add(item);
                        }
                        SaveData();
                        player.ChatMessage(Lang("BlockedItemsEditSuccess", player.UserIDString));
                        break;
                    case "unblk":
                    case "unblockitem":
                        if (args.Length == 2)
                        {
                            string item = Convert.ToString(args[1]);
                            blockedItemsList.Remove(item);
                        }
                        SaveData();
                        player.ChatMessage(Lang("BlockedItemsEditSuccess", player.UserIDString));
                        break;
                    case "blklist":
                    case "blockitemlist":
                        string itemlist = string.Join(", ", blockedItemsList);
                        if (debug) Puts("Items: " + itemlist);
                        player.ChatMessage(Lang("BlockedItems", player.UserIDString, itemlist));
                        break;
                    case "blkclr":
                    case "blockitemclear":
                        blockedItemsList.Clear();
                        SaveData();
                        player.ChatMessage(Lang("BlockedItemsClearSuccess", player.UserIDString));
                        break;
                }
                return;
            }
            else
            {
                player.ChatMessage(Lang("UsageAdmin", player.UserIDString));
                return;
            }
        }

        #endregion

        #region Helpers

        private string GetLordZoneId(string[] zids)
        {
            string czd = "None";
            for (int i = 0; i < zids.Length; i++)
            {
                string zonename = (string)ZoneManager.Call("GetZoneName", zids[i]);
                if (debug) Puts("Found zone " + zonename);
                if (zonename.StartsWith(zonenameprefix))
                {
                    if (debug) Puts("Landlord zone found");
                    czd = zids[i];
                }
            }
            return czd;
        }

        private static string GetGrid(Vector3 position)
        {
            var chars = new string[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "AA", "AB", "AC", "AD", "AE", "AF", "AG", "AH", "AI", "AJ", "AK", "AL", "AM", "AN", "AO", "AP", "AQ", "AR", "AS", "AT", "AU", "AV", "AW", "AX", "AY", "AZ" };

            const float block = 146;

            float size = ConVar.Server.worldsize;
            float offset = size / 2;

            float xpos = position.x + offset;
            float zpos = position.z + offset;

            int maxgrid = (int)(size / block);

            float xcoord = Mathf.Clamp(xpos / block, 0, maxgrid - 1);
            float zcoord = Mathf.Clamp(maxgrid - (zpos / block), 0, maxgrid - 1);

            string pos = string.Concat(chars[(int)xcoord], (int)zcoord);

            return pos;
        }

        private bool CheckConnected(string zid, ulong teampid)
        {
            ulong uzid = Convert.ToUInt64(zid);
            ulong[] uzids = new ulong[] { uzid + 1, uzid - 1, uzid + 1000, uzid - 1000 };
            for (int i = 0; i < uzids.Length; i++)
            {
                var match = quadrants[teampid].FirstOrDefault(stringToCheck => stringToCheck.Contains(Convert.ToString(uzids[i])));
                if (match != null) return true;
            }
            return false;
        }

        #endregion
    }
}
