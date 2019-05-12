/*
 * Description:             GameVersionConfigManager.cs
 * Author:                  TONYTANG
 * Create Date:             2018/08/12
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;

/// <summary>
/// VersionConfigModuleManager.cs
/// 游戏版本信息管理模块单例类
/// </summary>
public class VersionConfigModuleManager : SingletonMonoBehaviourTemplate<VersionConfigModuleManager>, IModuleInterface
{
    /// <summary>
    /// 模块名
    /// </summary>
    public string ModuleName
    {
        get
        {
            return this.GetType().ToString();
        }
    }

    /// <summary> 包外资源版本信息文件存储目录路径 /// </summary>
    public string OutterVersionConfigSaveFileFolderPath;

    /// <summary>
    /// 游戏版本信息配置文件名
    /// </summary>
    private const string mVersionConfigFileName = "VersionConfig";

    /// <summary>
    /// 配置文件目录路径
    /// </summary>
    private const string ConfigFolderPath = "Config/";

    /// <summary>
    /// 包内版本信息文件存储路径
    /// </summary>
    private string mInnerVersionConfigFilePath;

    /// <summary> 包外资源版本信息文件存储路径 /// </summary>
    private string OutterVersionConfigSaveFileFullPath;

    /// <summary>
    /// 游戏版本信息
    /// </summary>
    public VersionConfig GameVersionConfig
    {
        get;
        private set;
    }

    /// <summary>
    /// 包内版本信息
    /// </summary>
    private VersionConfig mInnerGameVersionConfig;

    /// <summary>
    /// 包外版本信息
    /// </summary>
    private VersionConfig mOuterGameVersionConfig;

    /// <summary>
    /// UTF8编码
    /// </summary>
    private UTF8Encoding mUTF8Encoding = new UTF8Encoding(true);

    private void Awake()
    {
        mInnerVersionConfigFilePath = ConfigFolderPath + mVersionConfigFileName;
        OutterVersionConfigSaveFileFolderPath = Application.persistentDataPath + "/" + ConfigFolderPath;
        OutterVersionConfigSaveFileFullPath = OutterVersionConfigSaveFileFolderPath + mVersionConfigFileName + ".json";
        GameVersionConfig = null;
        mInnerGameVersionConfig = null;
        mOuterGameVersionConfig = null;
    }

    /// <summary>
    /// 存储最新版本号信息
    /// </summary>
    /// <param name="versioncode">版本号</param>
    public void saveNewVersionCodeConfig(double versioncode)
    {
        //TODO:包外版本信息存储
        Debug.Log(string.Format("VersionConfigSaveFileFullPath : {0}", OutterVersionConfigSaveFileFullPath));

        if (GameVersionConfig == null)
        {
            Debug.LogError("找不到版本信息!无法存储新的版本信息!");
            return;
        }

        if (!Directory.Exists(OutterVersionConfigSaveFileFolderPath))
        {
            Directory.CreateDirectory(OutterVersionConfigSaveFileFolderPath);
        }

        GameVersionConfig.VersionCode = versioncode;
        Debug.Log("newverisoncode = " + versioncode);

        var versionconfigdata = JsonUtility.ToJson(GameVersionConfig);
        using (var verisionconfigfs = File.Open(OutterVersionConfigSaveFileFullPath, FileMode.Create))
        {
            byte[] versionconfiginfo = mUTF8Encoding.GetBytes(versionconfigdata);
            verisionconfigfs.Write(versionconfiginfo, 0, versionconfiginfo.Length);
            verisionconfigfs.Close();
        }
    }

    /// <summary>
    /// 存储最新资源版本号信息
    /// </summary>
    /// <param name="resourceversioncode">资源版本号</param>
    public void saveNewResoueceCodeConfig(int resourceversioncode)
    {
        //TODO:包外版本信息存储
        Debug.Log(string.Format("VersionConfigSaveFileFullPath : {0}", OutterVersionConfigSaveFileFullPath));

        if (GameVersionConfig == null)
        {
            Debug.LogError("找不到包内版本信息!无法存储新的版本信息!");
            return;
        }

        if(!Directory.Exists(OutterVersionConfigSaveFileFolderPath))
        {
            Directory.CreateDirectory(OutterVersionConfigSaveFileFolderPath);
        }

        GameVersionConfig.ResourceVersionCode = resourceversioncode;
        Debug.Log("newresourceversioncode = " + resourceversioncode);

        var versionconfigdata = JsonUtility.ToJson(GameVersionConfig);
        using (var verisionconfigfs = File.Open(OutterVersionConfigSaveFileFullPath, FileMode.Create))
        {
            byte[] versionconfiginfo = mUTF8Encoding.GetBytes(versionconfigdata);
            verisionconfigfs.Write(versionconfiginfo, 0, versionconfiginfo.Length);
            verisionconfigfs.Close();
        }
    }

    /// <summary>
    /// 初始化读取版本信息
    /// </summary>
    /// <returns></returns>
    public void initVerisonConfigData()
    {
        mInnerGameVersionConfig = null;
        mOuterGameVersionConfig = null;
        Debug.Log(string.Format("OutterVersionConfigSaveFileFullPath : {0}", OutterVersionConfigSaveFileFullPath));
        Debug.Log(string.Format("mInnerVersionConfigFilePath : {0}", mInnerVersionConfigFilePath));

        //读取包外版本信息
        if (File.Exists(OutterVersionConfigSaveFileFullPath))
        {
            var outterbytes = File.ReadAllBytes(OutterVersionConfigSaveFileFullPath);
            Debug.Log("包外版本信息:");
            var content = mUTF8Encoding.GetString(outterbytes);
            Debug.Log(string.Format("content : {0}", content));
            mOuterGameVersionConfig = JsonUtility.FromJson<VersionConfig>(content);
            Debug.Log(string.Format("VersionCode : {0} ResourceVersionCode : {1}", mOuterGameVersionConfig.VersionCode, mOuterGameVersionConfig.ResourceVersionCode));
        }
        else
        {
            Debug.Log(string.Format("包外游戏配置版本信息文件 : {0}不存在!读取包内资源版本信息!", OutterVersionConfigSaveFileFullPath));
        }

        //读取包内信息
        Debug.Log(string.Format("包内游戏配置版本信息文件 : {0}!", mInnerVersionConfigFilePath));
        //读取包内的版本信息
        var versionconfigasset = Resources.Load<TextAsset>(mInnerVersionConfigFilePath);
        if (versionconfigasset != null)
        {
            Debug.Log("包内版本信息:");
            var content = mUTF8Encoding.GetString(versionconfigasset.bytes);
            Debug.Log(string.Format("content : {0}", content));
            mInnerGameVersionConfig = JsonUtility.FromJson<VersionConfig>(content);
            Debug.Log(string.Format("VersionCode : {0} ResourceVersionCode : {1}", mInnerGameVersionConfig.VersionCode, mInnerGameVersionConfig.ResourceVersionCode));
        }
        else
        {
            Debug.LogError(string.Format("严重错误！包内游戏配置版本信息文件 : {0}不存在!无法读取!", mInnerVersionConfigFilePath));
        }

        //当前版本信息，如果包内比包外游戏版本号高，以包内为准
        //如果包内小于等于包外游戏版本号，以包外的为准
        GameVersionConfig = GameVersionConfig == null ? new VersionConfig() : GameVersionConfig;
        if (mOuterGameVersionConfig != null)
        {
            if(mInnerGameVersionConfig.VersionCode > mOuterGameVersionConfig.VersionCode)
            {
                GameVersionConfig.VersionCode = mInnerGameVersionConfig.VersionCode;
                GameVersionConfig.ResourceVersionCode = mInnerGameVersionConfig.ResourceVersionCode;
            }
            else
            {
                GameVersionConfig.VersionCode = mOuterGameVersionConfig.VersionCode;
                GameVersionConfig.ResourceVersionCode = mOuterGameVersionConfig.ResourceVersionCode;
            }
        }
        else
        {
            GameVersionConfig.VersionCode = mInnerGameVersionConfig.VersionCode;
            GameVersionConfig.ResourceVersionCode = mInnerGameVersionConfig.ResourceVersionCode;
        }
    }

    /// <summary>
    /// 是否需要版本强更
    /// </summary>
    /// <param name="newversioncode">新版本号</param>
    /// <returns></returns>
    public bool needVersionHotUpdate(double newversioncode)
    {
        return newversioncode > GameVersionConfig.VersionCode;
    }

    /// <summary>
    /// 是否需要版本强更
    /// </summary>
    /// <param name="newresourcecode">新资源版本号</param>
    /// <returns></returns>
    public bool needResourceHotUpdate(int newresourcecode)
    {
        return newresourcecode > GameVersionConfig.ResourceVersionCode;
    }

    /// <summary>
    /// 是否已经版本强更完成
    /// 判定包内版本号是否大于包外版本号
    /// </summary>
    /// <returns></returns>
    public bool hasVersionHotUpdate()
    {
        if(mOuterGameVersionConfig != null && mInnerGameVersionConfig.VersionCode > mOuterGameVersionConfig.VersionCode)
        {
            Debug.Log(string.Format("包内版本号 : {0} 包外版本号 : {1}", mInnerGameVersionConfig.VersionCode, mOuterGameVersionConfig.VersionCode));
            return true;
        }
        else
        {
            return false;
        }
    }
}