using System.Numerics;
using ECommons;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using Dalamud.Utility.Numerics;
using KodakkuAssist.Module.Draw.Manager;
using UsamisKodakku.Scripts.FolderName.SubFolderName;

namespace UsamisKodakku.Scripts._00_Other;

public class ExaflareModule
{
    public class Exaflare
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public ScriptAccessory accessory {get; set;} = null!;
        private readonly List<Vector3> _exaflarePos = [];
        private Vector3 _startPos = new(0, 0, 0);
        private float _scale;
        private int _extendNum;
        private float _extendDistance;
        private float _rotation;
        private int _advWarnNum;
        private int _intervalTime;
        private int _castTime;
        private Vector4 _exaflareColor = new(0, 0, 0, 0);
        private Vector4 _exaflareWarnColor = new(0, 0, 0, 0);

        public void Init(ScriptAccessory _accessory)
        {
            accessory = _accessory;
            _exaflarePos.Clear();
            _scale = 0;
            _startPos = new(0, 0, 0);
            _extendNum = 0;
            _extendDistance = 0;
            _rotation = 0;
            _advWarnNum = 0;
            _intervalTime = 0;
            _castTime = 0;
            _exaflareColor = accessory.Data.DefaultDangerColor;
            _exaflareWarnColor = accessory.Data.DefaultDangerColor;
        }
        
        private Vector3 GetExaflarePos(int extendIdx)
        {
            return _startPos.ExtendPoint(_rotation, _extendDistance * extendIdx);
        }
        
        public void BuildExaflareList()
        {
            for (var i = 0; i < _extendNum; i++)
                _exaflarePos.Add(GetExaflarePos(i));
        }
        
        public List<DrawPropertiesEdit> GetExaflareScene(bool draw)
        {
            var exaflareScene = new List<DrawPropertiesEdit>();
            for (var ext = 0; ext < _extendNum; ext++)
            {
                var destroy = ext == 0 ? _castTime : _intervalTime;
                var delay= ext == 0 ? 0 : _castTime + (ext - 1) * _intervalTime;
                var dp = GetExaflareDp(_exaflarePos[ext], delay, destroy);
                exaflareScene.Add(dp);
                if (draw)
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
            return exaflareScene;
        }
        
        public List<DrawPropertiesEdit> GetExaflareWarnScene(bool draw)
        {
            if (_advWarnNum == 0) return [];
            var exaflareWarnScene = new List<DrawPropertiesEdit>();
            for (var ext = 0; ext < _extendNum; ext++)
            {
                var destroy = ext == 0 ? _castTime : _intervalTime;
                var delay= ext == 0 ? 0 : _castTime + (ext - 1) * _intervalTime;
                for (var adv = 1; adv <= _advWarnNum; adv++)
                {
                    if (ext >= _exaflarePos.Count - adv) continue;
                    var dp = GetExaflareWarn(_exaflarePos[ext + adv], adv, delay, destroy, _intervalTime);
                    exaflareWarnScene.Add(dp);
                    if (draw)
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }
            }
            return exaflareWarnScene;
        }

        public List<DrawPropertiesEdit> GetExaflareEdge(bool draw)
        {
            var exaflareEdgeScene = new List<DrawPropertiesEdit>();
            for (var ext = 0; ext < _extendNum; ext++)
            {
                var destroy = ext == 0 ? _castTime : _intervalTime;
                var delay= ext == 0 ? 0 : _castTime + (ext - 1) * _intervalTime;
                var dp = GetExaflareEdge(_exaflarePos[ext], delay, destroy);
                exaflareEdgeScene.Add(dp);
                if (draw)
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp);
            }
            return exaflareEdgeScene;
        }
        
        private DrawPropertiesEdit GetExaflareDp(Vector3 pos, int delay, int destroy)
        {
            var color = _exaflareColor;
            var dp = accessory.DrawStaticCircle(pos, color, delay, destroy, $"地火{pos}", _scale);
            dp.ScaleMode |= ScaleMode.ByTime;
            return dp;
        }
        
        private DrawPropertiesEdit GetExaflareEdge(Vector3 pos, int delay, int destroy)
        {
            var color = _exaflareColor;
            var dp = accessory.DrawStaticDonut(pos, color, delay, destroy, $"地火边缘{pos}", _scale);
            // accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp);
            return dp;
        }
        
        private DrawPropertiesEdit GetExaflareWarn(Vector3 pos, int adv, int delay, int destroy, int interval)
        {
            var destroyItv = interval * (adv - 1);
            var color = _exaflareWarnColor.WithW(3f / 4f / adv);
            var dp = accessory.DrawStaticCircle(pos, color, delay, destroy + destroyItv, $"地火预警{pos}", _scale);
            return dp;
        }
        
        public void SetCastAndIntervalTime(int castTime, int intervalTime)
        {
            _castTime = castTime;
            _intervalTime = intervalTime;
        }

        public void SetExaflareExtension(int extendNum, float extendDistance)
        {
            _extendNum = extendNum;
            _extendDistance = extendDistance;
        }

        public void SetRotation(float rotation)
        {
            _rotation = rotation;
        }

        public void SetAdvanceWarnNum(int advWarnNum)
        {
            _advWarnNum = advWarnNum;
        }

        public void SetScale(float scale)
        {
            _scale = scale;
        }

        public void SetColor(Vector4 exaflareColor, Vector4 exaflareWarnColor)
        {
            _exaflareColor = exaflareColor;
            _exaflareWarnColor = exaflareWarnColor;
        }
    }
}


