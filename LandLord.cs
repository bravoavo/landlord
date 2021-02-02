using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Core;
using System;
using Oxide.Core.Configuration;


namespace Oxide.Plugins
{
    [Info("Landlord", "bravoavo", "1.0.7")]
    [Description("Take control of the map")]

    class LandLord : RustPlugin
    {

        [PluginReference] Plugin ZoneManager;
        ConfigData configData;
        private DynamicConfigFile data;

        //Constant initialization
        readonly uint bannerprefabid = 3188315846;
        readonly float size = 146.33f;
        readonly bool debug = false;
        readonly string zonenameprefix = "Zone";
        readonly string datafile_name = "Landlord.data";
        Color32 orange = new Color32(255, 157, 0, 1);
        readonly Color[] colorArray = new Color[] { Color.black, Color.white, Color.red, Color.blue, Color.green, Color.yellow, Color.cyan, Color.gray, new Color32(255, 157, 0, 1) };
        private Color colors;
        public Dictionary<ulong, HashSet<int[]>> flagi = new Dictionary<ulong, HashSet<int[]>>();
        public Dictionary<ulong, List<string>> quadrants = new Dictionary<ulong, List<string>>();
        public Dictionary<string, Vector3> poles = new Dictionary<string, Vector3>();
        public Dictionary<ulong, int> gatherMultiplier = new Dictionary<ulong, int>();
        public Dictionary<ulong, ulong> teamList = new Dictionary<ulong, ulong>();

        public Dictionary<string, MapMarkerGenericRadius> allmarkers = new Dictionary<string, MapMarkerGenericRadius>();
        public Dictionary<string, List<MapMarkerGenericRadius>> flagmarkers = new Dictionary<string, List<MapMarkerGenericRadius>>();

        #region Localization
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LordStatus"] = "<color=orange>LANDLORD:</color> Grounds seized: {0} TeamID is: {1}",
                ["LordStatusZero"] = "<color=orange>LANDLORD:</color> Grounds seized: 0 TeamID is: {0}",
                ["TeamClearSuccess"] = "<color=orange>LANDLORD:</color> Team clear: Success!",
                ["HaveNoTeam"] = "<color=orange>LANDLORD:</color> You are have no team!",
                ["CellHasPole"] = "<color=orange>LANDLORD:</color> This map cell already has a pole! Destroy it before build a new one",
                ["CellCaptured"] = "<color=orange>LANDLORD:</color> You deploy pole! Your gather rate increased!"
            }, this);
        }

        #endregion Localization

        #region ZoneInitialization
        private void MyZonesInit()
        {
            float mapsize = TerrainMeta.Size.x;
            int i = 1;
            for (var x = -mapsize / 2 + size / 2; x <= mapsize / 2; x = x + size)
            {
                for (var z = -mapsize / 2 + size / 2; z <= mapsize / 2; z = z + size)
                {
                    string[] array = new string[] { "name", "Main" + zonenameprefix + i, "size", "146.3 500 146.3", "location", x + " 0 " + z };
                    ZoneManager.Call("CreateOrUpdateZone", "10" + i, array);
                    i++;
                }
            }
            var InitFile = Interface.Oxide.DataFileSystem.GetFile(datafile_name);

            InitFile.Save();

        }
        #endregion

        #region InitPlugin
        private void OnServerInitialized()
        {
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
            LordTeamInit(player);
            foreach (var quadrat in quadrants)
            {
                foreach (var zone in quadrat.Value)
                {
                    allmarkers[zone].SendUpdate();
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

        private void LordTeamInit(BasePlayer player)
        {
            if (!teamList.ContainsKey(player.userID))
            {
                if (player.currentTeam > 0)
                {
                    //var teamelement = UnityEngine.Object.FindObjectOfType<RelationshipManager>();
                    //var team = teamelement.FindTeam(player.currentTeam);
                    RelationshipManager.PlayerTeam team = RelationshipManager.Instance.teams[player.currentTeam];
                    if (!teamList.ContainsKey(team.teamLeader))
                    {
                        teamList.Add(player.userID, team.teamLeader);

                        if (debug) Puts("You has team now!");

                        if (quadrants.ContainsKey(player.userID) && player.userID != team.teamLeader)
                        {
                            foreach (var zone in quadrants[player.userID])
                            {
                                DeleteMarkerFromMap(flagmarkers[zone][0]);
                                DeleteMarkerFromMap(flagmarkers[zone][1]);
                                DeleteMarkerFromMap(flagmarkers[zone][2]);
                                flagmarkers.Remove(zone);
                                if (!quadrants.ContainsKey(team.teamLeader))
                                {
                                    quadrants.Add(team.teamLeader, new List<string>());
                                    quadrants[team.teamLeader].Add(zone);
                                }
                                else
                                {
                                    var match = quadrants[team.teamLeader].FirstOrDefault(stringToCheck => stringToCheck.Contains(zone));
                                    if (match != null) return;
                                    else
                                    {
                                        quadrants[team.teamLeader].Add(zone);
                                    }
                                }

                                if (!gatherMultiplier.ContainsKey(team.teamLeader)) gatherMultiplier.Add(team.teamLeader, 1);
                                else gatherMultiplier[team.teamLeader]++;
                                var zonelocation = (Vector3)ZoneManager.Call("GetZoneLocation", zone);
                                SpawnFlag(zonelocation, zone, team.teamLeader);
                            }
                            quadrants.Remove(player.userID);
                            gatherMultiplier.Remove(player.userID);
                        }
                        SaveData();
                        return;
                    }
                    else
                    {

                        var currentLeader = teamList[team.teamLeader];
                        teamList.Add(player.userID, currentLeader);

                        if (debug) Puts("You has team now!");
                        if (quadrants.ContainsKey(player.userID))
                        {
                            foreach (var zone in quadrants[player.userID])
                            {
                                DeleteMarkerFromMap(flagmarkers[zone][0]);
                                DeleteMarkerFromMap(flagmarkers[zone][1]);
                                DeleteMarkerFromMap(flagmarkers[zone][2]);
                                flagmarkers.Remove(zone);
                                if (!quadrants.ContainsKey(currentLeader))
                                {
                                    quadrants.Add(currentLeader, new List<string>());
                                    quadrants[currentLeader].Add(zone);


                                }
                                else
                                {
                                    var match = quadrants[currentLeader].FirstOrDefault(stringToCheck => stringToCheck.Contains(zone));
                                    if (match != null) return;
                                    else
                                    {
                                        quadrants[currentLeader].Add(zone);

                                    }
                                }

                                if (!gatherMultiplier.ContainsKey(currentLeader)) gatherMultiplier.Add(currentLeader, 1);
                                else gatherMultiplier[currentLeader]++;
                                var zonelocation = (Vector3)ZoneManager.Call("GetZoneLocation", zone);
                                SpawnFlag(zonelocation, zone, currentLeader);
                            }
                            quadrants.Remove(player.userID);
                            gatherMultiplier.Remove(player.userID);
                        }
                        SaveData();
                        return;
                    }
                }
                else
                {
                    timer.In(5, () => LordTeamInit(player));
                    return;
                }
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
                    if (debug) Puts("Looking for zone id by coordinates. Zoneid is " + zoneid);
                    if (allmarkers.ContainsKey(zoneid))
                    {
                        DeleteMarkerFromMap(allmarkers[zoneid]);
                        DeleteMarkerFromMap(flagmarkers[zoneid][0]);
                        DeleteMarkerFromMap(flagmarkers[zoneid][1]);
                        DeleteMarkerFromMap(flagmarkers[zoneid][2]);
                        allmarkers.Remove(zoneid);
                        flagmarkers.Remove(zoneid);
                    }
                    if (debug) Puts("User ID " + entity.OwnerID);
                    ulong teampid;
                    if (teamList.ContainsKey(entity.OwnerID)) { teampid = teamList[entity.OwnerID]; }
                    else teampid = entity.OwnerID;
                    quadrants[teampid].Remove(zoneid);
                    poles.Remove(zoneid);
                    if (gatherMultiplier.ContainsKey(teampid))
                    {
                        if (gatherMultiplier[teampid] > 0) gatherMultiplier[teampid]--;
                    }
                    SaveData();
                }
                else if (debug) Puts("Zone id not found");
            }
        }


        void OnEntityBuilt(Planner plan, GameObject go)
        {
            BasePlayer player = plan.GetOwnerPlayer();
            string[] zids = (string[])ZoneManager.Call("GetPlayerZoneIDs", player);
            var entity = go.ToBaseEntity();
            if (entity.prefabID == bannerprefabid && zids.Length > 0)
            {
                if (debug) Puts("Entity build. Prefab ID is " + entity.prefabID + " zone ID is " + zids[0]);
                string curentZoneId = zids[0];
                if (poles.ContainsKey(curentZoneId))
                {
                    entity.Invoke(() => entity.Kill(BaseNetworkable.DestroyMode.Gib), 0.1f);
                    player.ChatMessage(Lang("CellHasPole", player.UserIDString));
                    return;
                }

                ulong teampid;
                if (teamList.ContainsKey(player.userID)) { teampid = teamList[player.userID]; }
                else teampid = player.userID;

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
                if (configData.PolesLocationData.ContainsKey(curentZoneId)) Puts("Pole exists");


                if (debug) Puts("Server position " + entity.ServerPosition.ToString());
                var zonelocation = (Vector3)ZoneManager.Call("GetZoneLocation", curentZoneId);
                SpawnMarkerOnMap(zonelocation, curentZoneId);
                SpawnFlag(zonelocation, curentZoneId, teampid);
                player.ChatMessage(Lang("CellCaptured", player.UserIDString));
                SaveData();
            }
            else if (debug) Puts("Error! Not found zone ID or prefab!");
        }

        void OnCollectiblePickup(Item item, BasePlayer player)
        {
            ulong teampid;
            if (teamList.ContainsKey(player.userID)) { teampid = teamList[player.userID]; }
            else teampid = player.userID;
            if (gatherMultiplier.ContainsKey(teampid))
            {
                if (gatherMultiplier[teampid] > 0)
                {
                    float multiplier = (float)gatherMultiplier[teampid] * 3 / 100;
                    item.amount = (int)(item.amount + 1 + item.amount * multiplier);
                }
            }
        }

        object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            ulong teampid;

            if (teamList.ContainsKey(player.userID)) { teampid = teamList[player.userID]; }
            else teampid = player.userID;
            if (debug) Puts("Dispenser. Start amount is " + item.amount);
            if (gatherMultiplier.ContainsKey(teampid))
            {
                if (gatherMultiplier[teampid] > 0)
                {
                    float multiplier = (float)gatherMultiplier[teampid] * 3 / 100;
                    if (debug) Puts("Multiplier is " + multiplier);
                    item.amount = (int)(item.amount + 5 + item.amount * multiplier);
                }
            }
            return null;
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
            public Dictionary<ulong, ulong> TeamListData = new Dictionary<ulong, ulong>();
        }

        private void LoadData()
        {
            quadrants.Clear();
            flagi.Clear();
            gatherMultiplier.Clear();
            teamList.Clear();
            poles.Clear();
            try
            {
                if (debug) Puts("A try to use existing configuration");
                configData = data.ReadObject<ConfigData>();
                quadrants = configData.QuadrantsData;
                flagi = configData.FlagiData;
                gatherMultiplier = configData.GatherRateData;
                teamList = configData.TeamListData;
                poles = configData.PolesLocationData;
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
                            SpawnMarkerOnMap(zonelocation, zone);
                            SpawnFlag(zonelocation, zone, quadrat.Key);
                        }
                    }

                }
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
            }

        }

        private void SaveData()
        {
            data = Interface.Oxide.DataFileSystem.GetDatafile(datafile_name);
            data.WriteObject(configData);
        }

        #endregion

        #region ChatCommands
        [ChatCommand("lord")]
        private void CmdChatLordStats(BasePlayer player, string command, string[] args)
        {
            ulong teampid;
            if (teamList.ContainsKey(player.userID)) teampid = teamList[player.userID];
            else teampid = player.userID;

            if (gatherMultiplier.ContainsKey(teampid) && gatherMultiplier[teampid] > 0)
            {
                player.ChatMessage(Lang("LordStatus", player.UserIDString, gatherMultiplier[teampid].ToString(), teampid.ToString()));
            }
            else player.ChatMessage(Lang("LordStatusZero", player.UserIDString, teampid.ToString()));

        }
        [ChatCommand("lordclr")]
        private void CmdChatLordCls(BasePlayer player, string command, string[] args)
        {
            if (teamList.ContainsKey(player.userID))
            {
                teamList.Remove(player.userID);
                player.ChatMessage(Lang("TeamClearSuccess", player.UserIDString));
                LordTeamInit(player);
            }
            else player.ChatMessage(Lang("HaveNoTeam", player.UserIDString));
        }
        #endregion

    }
}