/*
 * Lone EFT DMA Radar - Copyright (c) 2026 Lone DMA
 * Licensed under GNU AGPLv3. See https://www.gnu.org/licenses/agpl-3.0.html
 */
using LoneEftDmaRadar.Tarkov.World.Player;
using MemoryPack;

namespace LoneEftDmaRadar.Web.WebRadar.Data
{
    [MemoryPackable]
    public partial struct WebRadarPlayer
    {
        /// <summary>
        /// Player Name.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Player Type (PMC, Scav,etc.)
        /// </summary>
        public WebPlayerType Type { get; set; }
        /// <summary>
        /// True if player is active, otherwise False.
        /// </summary>
        public bool IsActive { get; set; }
        /// <summary>
        /// True if player is alive, otherwise False.
        /// </summary>
        public bool IsAlive { get; set; }
        /// <summary>
        /// Unity World Position.
        /// </summary>
        public Vector3 Position { get; set; }
        /// <summary>
        /// Unity World Rotation.
        /// </summary>
        public Vector2 Rotation { get; set; }

        /// <summary>
        /// Create a WebRadarPlayer from a Full Player Object.
        /// </summary>
        /// <param name="player">Full EFT Player Object.</param>
        /// <returns>Compact WebRadarPlayer object.</returns>
        public static WebRadarPlayer Create(AbstractPlayer player)
        {
            WebPlayerType type = player is LocalPlayer ?
                WebPlayerType.LocalPlayer : player.IsFriendly ?
                WebPlayerType.Teammate : player.IsHuman ?
                player.IsScav ?
                WebPlayerType.PlayerScav : WebPlayerType.Player : WebPlayerType.Bot;
            return new WebRadarPlayer
            {
                Name = player.Name,
                Type = type,
                IsActive = player.IsActive,
                IsAlive = player.IsAlive,
                Position = player.Position,
                Rotation = player.Rotation
            };
        }
    }
}

