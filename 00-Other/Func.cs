using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Network.Structures.InfoProxy;
using Newtonsoft.Json;
using Dalamud.Utility.Numerics;
using ECommons;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Lumina.Excel.GeneratedSheets2;

namespace UsamisUsefulFunc
{
    public class Func
    {
        /// <summary>
        /// �����ط�Ϊ�����ޣ�����Ϊ0������Ϊ1������Ϊ2������Ϊ3
        /// </summary>
        /// <param name="point"></param>
        /// <param name="centre"></param>
        /// <returns></returns>
        private int PositionFloorTo4Dir(Vector3 point, Vector3 centre)
        {
            // Dirs: NE = 0, SE = 1, SW = 2, NW = 3
            var r = Math.Floor(2 - 2 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 4;
            return (int)r;
        }

        /// <summary>
        /// ��������X�ַ�Ϊ�����ޣ���Ϊ0����Ϊ1����Ϊ2����Ϊ3
        /// </summary>
        /// <param name="point"></param>
        /// <param name="centre"></param>
        /// <returns></returns>
        private int PositionRoundTo4Dir(Vector3 point, Vector3 centre)
        {
            // Dirs: N = 0, E = 1, S = 2, W = 3
            var r = Math.Round(2 - 2 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 4;
            return (int)r;
        }

        /// <summary>
        /// ÿ45��Ϊ1�񣬴�������ʼ˳ʱ�룬0~7
        /// </summary>
        /// <param name="point"></param>
        /// <param name="centre"></param>
        /// <returns></returns>
        private int PositionTo8Dir(Vector3 point, Vector3 centre)
        {
            // Dirs: N = 0, NE = 1, ..., NW = 7
            var r = Math.Round(4 - 4 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 8;
            return (int)r;

        }

        /// <summary>
        /// ÿ30��Ϊ1�񣬴�������ʼ˳ʱ�룬0~11
        /// </summary>
        /// <param name="point"></param>
        /// <param name="centre"></param>
        /// <returns></returns>
        private int PositionTo12Dir(Vector3 point, Vector3 centre)
        {
            // Dirs: N = 0, NE = 1, ..., NW = 7
            var r = Math.Round(6 - 6 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 12;
            return (int)r;

        }

        /// <summary>
        /// Χ��ĳ����ת�ض��Ƕ�
        /// </summary>
        /// <param name="point"></param>
        /// <param name="centre"></param>
        /// <param name="radian"></param>
        /// <returns></returns>
        private Vector3 RotatePoint(Vector3 point, Vector3 centre, float radian)
        {

            Vector2 v2 = new(point.X - centre.X, point.Z - centre.Z);

            var rot = (MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian);
            var lenth = v2.Length();
            return new(centre.X + MathF.Sin(rot) * lenth, centre.Y, centre.Z - MathF.Cos(rot) * lenth);
        }

    }
}