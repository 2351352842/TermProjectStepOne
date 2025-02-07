using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
public enum ErrorCode
{
    DownloadContentEmpty,
    TempFileMissing
}
/// <summary>
/// 下载错误回调
/// </summary>
/// <param name="errorCode">错误码</param>
/// <param name="message">错误信息</param>
public delegate void ErrorEventHandler(ErrorCode errorCode, string message);
/// <summary>
/// 下载完成回调
/// </summary>
/// <param name="message">完成的消息</param>
public delegate void CompletedEventHandler(string fileName,string message);
/// <summary>
/// 下载进度回调
/// </summary>
/// <param name="prg">当前进度</param>
/// <param name="currLength">当前下载完成的长度</param>
/// <param name=""></param>
/// <param name="totalLength">文件总长度</param>
public delegate void ProgressEventHandler(float prg, long currLength, long totalLength);
public class DownloadHandler : DownloadHandlerScript
{
    private string savePath = null; // 保存到的路径
    private string tempPath = null;// 下载临时文件路径
    private long currLength = 0;// 当前已经下载的数据长度
    private long totalLength = 0; // 文件总数据长度
    private long contentLength = 0;// 本次需要下载的数据长度
    private FileStream fileStream = null;// 文件流，用来将接收到的数据写入文件
    private ErrorEventHandler onError = null; // 出错回调
    private CompletedEventHandler onCompleted = null; // 
    private ProgressEventHandler onProgress = null; // 进度回调
    public long CurrLength
    {
        get { return currLength; }
    }
    public long TotalLength
    {
        get { return totalLength; }
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="savePath">下载后文件的保存地址</param>
    /// <param name="onCompleted">下载完成的回调<</param>
    /// <param name="onProgress">正在下载时的回调</param>
    /// <param name="onError"></param>
    public DownloadHandler(string savePath, CompletedEventHandler onCompleted, ProgressEventHandler onProgress, ErrorEventHandler onError) : base(new byte[1024 * 1024])
    {
        this.savePath = savePath.Replace("\\", " /");
        this.onCompleted = onCompleted;
        this.onProgress = onProgress;
        this.onError = onError;
        this.tempPath = savePath + ".temp";
        //找到对应文件路径下的临时文件,使用文件流的方式访问
        fileStream = new FileStream(tempPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        //将当前长度更新为临时文件已写入的字节长度
        currLength = fileStream.Length;
        fileStream.Position = currLength;
    }
    /// <summary>
    /// 收到 Content-Length 标头调用的回调。
    /// </summary>
    /// <param name="contentLength">从文件的某个字节开始,到文件最后一个字节的长度</param>
    protected override void ReceiveContentLengthHeader(ulong contentLength)
    {
        this.contentLength = (long)contentLength;
        totalLength = this.contentLength + currLength;
    }
    /// <summary>
    /// 从远程服务器收到数据时调用的回调。
    /// </summary>
    /// <param name="data"></param>
    /// <param name="dataLength"></param>
    /// <returns></returns>
    protected override bool ReceiveData(byte[] data, int dataLength)
    {
        // 如果下载的数据长度小于等于0,就结束下载
        if (contentLength <= 0 || data == null || data.Length <= 0)
        {
            return false;
        }
        fileStream.Write(data, 0, dataLength);
        currLength += dataLength;
        onProgress?.Invoke(currLength * 1.0f / totalLength, currLength, totalLength);
        return true;
    }
    public override void Dispose()
    {
        base.Dispose();
        FileStreamClose();
    }
    public void FileStreamClose()
    {
        if (fileStream == null) return;
        fileStream.Close();
        fileStream.Dispose();
        fileStream = null;
        Debug.Log("文件流关闭");

    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="message">完成的消息</param>
    protected override void CompleteContent()
    {
        //接收完成所有数据后，首先关闭文件流
        FileStreamClose();
        // 如果服务器上不存在该文件，请求下载的内容长度会为0
        //所以需要特殊处理这种情况
        if (contentLength <= 0)
        {
            onError(ErrorCode.DownloadContentEmpty, "下载内容长度为0");
            return;
        }
        //如果下载完成后，临时文件如果被意外删除了，也抛出错误提示
        if (!File.Exists(tempPath))
        {
            onError(ErrorCode.TempFileMissing,"下载临时缓存文件丢失");
            return;
        }
        //如果下载的文件已经存在，就删除原文件
        if (File.Exists(savePath))
        {
            File.Delete(savePath);
        }
        //通过了以上的校验后，就将临时文件移动到目标路径，下载成功
        File.Move(tempPath, savePath);
        FileInfo fileInfo = new FileInfo(savePath);
        onCompleted(fileInfo.Name,"下载文件完成");
    }
}
public class Downloader
{
    private string url = null;// 需要下载的文件的地址
    private string savePath = null; // 保存的路径
    private UnityWebRequest request = null; // Unity中用来与Web服务器进行通信的类
    private DownloadHandler downloadHandler = null; // 我们自己实现的下载处理类
    private ErrorEventHandler onError = null; // 出错回调
    private CompletedEventHandler onCompleted = null; // 完成回调
    private ProgressEventHandler onProgress = null; // 进度回调
    /// <summary>
    /// 
    /// </summary>
    /// <param name="url">文件网络地址</param>
    /// <param name="savePath">文件本地保存地址</param>
    /// <param name="onCompleted">完成回调</param>
    /// <param name="onProgress">下载时回调</param>
    /// <param name="onError">报错回调</param>
    public Downloader(string url, string savePath, CompletedEventHandler onCompleted, ProgressEventHandler onProgress, ErrorEventHandler onError)
    {
        this.url = url;
        this.savePath = savePath;
        this.onCompleted = onCompleted;
        this.onProgress = onProgress;
        this.onError = onError;
    }
    public void Start(int timeout = 10)
    {
        request = UnityWebRequest.Get(url);
        if (!string.IsNullOrEmpty(savePath))
        {
            request.timeout = timeout;
            request.disposeDownloadHandlerOnDispose = true;
            downloadHandler = new DownloadHandler(savePath, onCompleted, onProgress, onError);
            // 这里是设置http的请求头
            //range表示请求资源的部分内容（不包括响应头的大小），单位是byte
            request.SetRequestHeader("range", $"bytes={downloadHandler.CurrLength}-");
            request.downloadHandler = downloadHandler;
        }
        request.SendWebRequest();
    }
    public void Dispose()
    {
        onError = null;
        onCompleted = null;
        onProgress = null;
        if (request != null)
        {
            if (!request.isDone)
            {
                request.Abort();
                request.Dispose();
                request = null;
            }
        }
    }
}
public class DownloadInfo
{
    /// <summary>
    /// 已经下载的文件名
    /// </summary>
    /// <typeparam name=""></typeparam>
    /// <param name=""></param>
    /// <param name=""></param>
    /// <param name=""></param>
    /// <returns></returns>
    public List<string> DownloadFileNames= new List<string>();
}
