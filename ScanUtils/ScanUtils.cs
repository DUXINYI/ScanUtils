using NTwain;
using NTwain.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace ScanUtils
{

    public static class ScanUtils
    {
        static void Release()
        {
            Monitor.Enter(_lockObj);
            if (_session.CurrentSource != null)
                _session.CurrentSource.Close();
            if (_session.State != State.DsmLoaded.GetHashCode())
                _session.Close();
            _scanResult = null;
            _cancelFlag = false;
            Monitor.Pulse(_lockObj);
            Monitor.Exit(_lockObj);
            Monitor.Exit(_session);
        }
        static TwainSession _session = new TwainSession(TWIdentity.CreateFromAssembly(DataGroups.Image, Assembly.GetExecutingAssembly()));
        static ScanUtils()
        {
            _session.TransferReady += (s, e) =>
            {
                if(_cancelFlag || _scanResult == null)
                {//扫描请求被取消
                    e.CancelAll = true;
                }
            };
            _session.DataTransferred += (s, e) =>
            {
                if (_cancelFlag)
                    return;
                var filename = Path.Combine(_outPath, Path.GetRandomFileName());
                using (var fs = File.OpenWrite(filename)) {

                    e.GetNativeImageStream().CopyTo(fs);
                    _scanResult?.Add(filename);
                }
            };
            _session.TransferError += (s, e) =>
            {
                Monitor.Enter(_lockObj);
                _scanResult = null;
                Monitor.Pulse(_lockObj);
                Monitor.Exit(_lockObj);
            };
            _session.SourceDisabled += (s, e) =>
            {//最后一张
                Monitor.Enter(_lockObj);
                Monitor.Pulse(_lockObj);
                Monitor.Exit(_lockObj);
            };
        }
        #region 设备信息
        /// <summary>
        /// 查找扫描仪驱动对象
        /// </summary>
        /// <returns>扫描仪驱动列表</returns>
        public static IEnumerable<DataSource> DicoverDrivers()
        {
            if (_session.State != State.DsmLoaded.GetHashCode())
                return null;
            try
            {
                Monitor.Enter(_session);
                var rcode = _session.Open();
                if (rcode != ReturnCode.Success)
                    return null;
                var retval = _session.ToList();
                return retval;
            }
            finally
            {
                Release();
            }
        }
        /// <summary>
        /// 检测扫描仪的状态
        /// </summary>
        /// <param name="deviceName">设备名称</param>
        /// <returns>true-设备正常；false-设备离线/故障</returns>
        public static bool CheckScanner(string deviceName)
        {
            if (_session.State != State.DsmLoaded.GetHashCode())
                return false;
            try
            {
                Monitor.Enter(_session);
                var rcode = _session.Open();
                if (rcode != ReturnCode.Success)
                    throw new IOException(rcode.ToString());
                rcode = _session.OpenSource(deviceName);
                var retval = rcode == ReturnCode.Success;
                return retval;
            }
            finally
            {
                Release();

            }
        }
        #endregion
        #region 扫描
        static object _lockObj = new object();
        static List<string> _scanResult;
        static bool _cancelFlag = false;
        static string _outPath = null;
        /// <summary>
        /// 停止扫描活动
        /// </summary>
        public static void StopScan()
        {
            if (!_session.IsSourceEnabled)
                return;
            _cancelFlag = true;
        }
        public static IList<string> StartScanImpl(string deviceName,string outPath,int dpi,bool isColor, SupportedSize size, OrientationType direction,bool isDouble)
        {
            if (_session.State != State.DsmLoaded.GetHashCode())
                return null;
            try
            {
                Monitor.Enter(_session);
                var rcode = _session.Open();
                if (rcode != ReturnCode.Success)
                    throw new IOException(rcode.ToString());
                rcode = _session.OpenSource(deviceName);
                if (rcode != ReturnCode.Success)
                    throw new IOException(rcode.ToString());
                _outPath = outPath ?? Path.GetTempPath();
                if (!Directory.Exists(_outPath))
                    Directory.CreateDirectory(_outPath);
                //DPI
                _session.CurrentSource.Capabilities.ICapXResolution.SetValue(dpi);
                _session.CurrentSource.Capabilities.ICapYResolution.SetValue(dpi);
                //彩色
                _session.CurrentSource.Capabilities.ICapPixelType.SetValue(isColor ? PixelType.RGB : PixelType.Gray);
                //纸张大小
                _session.CurrentSource.Capabilities.ICapSupportedSizes.SetValue(size);
                //方向
                _session.CurrentSource.Capabilities.ICapOrientation.SetValue(direction);
                //双面
                _session.CurrentSource.Capabilities.CapDuplexEnabled.SetValue(isDouble ? BoolType.True : BoolType.False);
                //高扫

                _scanResult = new List<string>();
                rcode = _session.CurrentSource.Enable(SourceEnableMode.NoUI, false, IntPtr.Zero);
                if (rcode == ReturnCode.Success)
                {
                    Monitor.Enter(_lockObj);
                    Monitor.Pulse(_lockObj);
                    Monitor.Wait(_lockObj);
                    Monitor.Exit(_lockObj);
                }
                else
                    throw new IOException(rcode.ToString());
                var retval = _scanResult;
                _scanResult = null;
                return retval;
            }
            finally
            {
                Release();

            }
        }
        public static Task<IList<string>> StartScan(string deviceName,string outPath,int dpi=300,bool isColor=false, SupportedSize size= SupportedSize.A4, OrientationType direction= OrientationType.Auto,bool isDouble=true)
        {
            return Task.Run(() => StartScanImpl(deviceName, outPath,dpi,isColor,size,direction,isDouble));
        }
        #endregion
    }
}
