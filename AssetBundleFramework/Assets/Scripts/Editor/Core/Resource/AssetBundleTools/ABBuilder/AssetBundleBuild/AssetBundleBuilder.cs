﻿//--------------------------------------------------
// Motion Framework
// Copyright©2018-2020 何冠峰
// Licensed under the MIT license
//--------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Security.Cryptography;
using TResource;

namespace MotionFramework.Editor
{
	public class AssetBundleBuilder
	{
		/// <summary>
		/// AssetBundle压缩选项
		/// </summary>
		public enum ECompressOption
		{
			Uncompressed = 0,
			StandardCompressionLZMA,
			ChunkBasedCompressionLZ4,
		}

		/// <summary>
		/// 输出的根目录
		/// </summary>
		private readonly string _outputRoot;

		/// <summary>
		/// 构建平台
		/// </summary>
		public BuildTarget BuildTarget { private set; get; } = BuildTarget.NoTarget;

		/// <summary>
		/// 输出目录
		/// </summary>
		public string OutputDirectory { private set; get; } = string.Empty;

		/// <summary>
		/// 构建选项
		/// </summary>
		public ECompressOption CompressOption = ECompressOption.Uncompressed;

		/// <summary>
		/// 是否强制重新打包资源
		/// </summary>
		public bool IsForceRebuild = false;

		/// <summary>
		/// 
		/// </summary>
		public bool IsAppendHash = false;

		/// <summary>
		/// 
		/// </summary>
		public bool IsDisableWriteTypeTree = false;

		/// <summary>
		/// 
		/// </summary>
		public bool IsIgnoreTypeTreeChanges = false;

		/// <summary>
		/// AssetBuilder
		/// </summary>
		/// <param name="buildTarget">构建平台</param>
		/// <param name="buildVersion">构建版本号</param>
		/// <param name="resourceVersion">资源版本号</param>
		public AssetBundleBuilder(BuildTarget buildTarget)
		{
			_outputRoot = AssetBundleBuilderHelper.GetDefaultOutputRootPath();
			BuildTarget = buildTarget;
			OutputDirectory = GetOutputDirectory();
		}

		/// <summary>
		/// 准备构建
		/// </summary>
		public void PreAssetBuild()
		{
			Debug.Log("------------------------------OnPreAssetBuild------------------------------");

			// 检测构建平台是否合法
			if (BuildTarget == BuildTarget.NoTarget)
				throw new Exception("[BuildPatch] 请选择目标平台");

			// 检测构建版本是否合法
			//if (EditorUtilities.IsNumber(BuildVersion.ToString()) == false)
			//	throw new Exception($"[BuildPatch] 版本号格式非法：{BuildVersion}");
			//if (BuildVersion < 0)
			//	throw new Exception("[BuildPatch] 请先设置版本号");

			// 检测输出目录是否为空
			if (string.IsNullOrEmpty(OutputDirectory))
				throw new Exception("[BuildPatch] 输出目录不能为空");

			// 检测补丁包是否已经存在
			//string packageDirectory = GetPackageDirectory();
			//if (Directory.Exists(packageDirectory))
			//	throw new Exception($"[BuildPatch] 补丁包已经存在：{packageDirectory}");

			// 如果是强制重建
			if (IsForceRebuild)
			{
				// 删除平台总目录
				string platformDirectory = $"{_outputRoot}/{BuildTarget}";
				if (Directory.Exists(platformDirectory))
				{
					Directory.Delete(platformDirectory, true);
					Log($"删除平台总目录：{platformDirectory}");
				}
			}

			// 如果输出目录不存在
			if (Directory.Exists(OutputDirectory) == false)
			{
				Directory.CreateDirectory(OutputDirectory);
				Log($"创建输出目录：{OutputDirectory}");
			}

            // Asset打包信息输出目录不存在
            var assetbuildinfofolderpath = Application.dataPath + ResourceConstData.AssetBuildInfoAssetRelativePath;
            Debug.Log($"Asset打包信息输出目录:{assetbuildinfofolderpath}");
            if (!Directory.Exists(assetbuildinfofolderpath))
            {
                Directory.CreateDirectory(assetbuildinfofolderpath);
                Log($"创建打包信息Asset输出目录：{assetbuildinfofolderpath}");
            }
		}

		/// <summary>
		/// 执行构建
		/// </summary>
		public void PostAssetBuild()
		{
			Debug.Log("------------------------------OnPostAssetBuild------------------------------");

			// 准备工作
			List<AssetBundleBuild> buildInfoList = new List<AssetBundleBuild>();
            Dictionary<string, AssetBundleBuildInfo> abbuildinfomap = new Dictionary<string, AssetBundleBuildInfo>();
			List<AssetInfo> buildMap = GetBuildMap(ref abbuildinfomap);
			if (buildMap.Count == 0)
				throw new Exception("[BuildPatch] 构建列表不能为空");

			Log($"构建列表里总共有{buildMap.Count}个资源需要构建");
			for (int i = 0; i < buildMap.Count; i++)
			{
            	AssetInfo assetInfo = buildMap[i];
				AssetBundleBuild buildInfo = new AssetBundleBuild();
                buildInfo.assetBundleName = assetInfo.AssetBundleLabel;
                buildInfo.assetBundleVariant = assetInfo.AssetBundleVariant;
                buildInfo.assetNames = new string[] { assetInfo.AssetPath };
                buildInfoList.Add(buildInfo);
            }
            // AssetBuildInfoAsset打包信息单独打包
            var assetbuildinfoassetrelativepath = $"Assets{ResourceConstData.AssetBuildInfoAssetRelativePath}/{ResourceConstData.AssetBuildInfoAssetName}.asset";
            var buildinfo = new AssetBundleBuild();
            buildinfo.assetBundleName = ResourceConstData.AssetBuildInfoAssetName;
            buildinfo.assetBundleVariant = ResourceConstData.AssetBundleDefaultVariant;
            buildinfo.assetNames = new string[] { assetbuildinfoassetrelativepath };
            buildInfoList.Add(buildinfo);
            abbuildinfomap.Add(assetbuildinfoassetrelativepath, new AssetBundleBuildInfo(ResourceConstData.AssetBuildInfoAssetName.ToLower()));

            // 更新AB打包信息Asset(e.g.比如Asset打包信息, AB打包依赖信息)
            UpdateAssetBundleBuildInfoAsset(buildInfoList, abbuildinfomap);

            // 开始构建
            Log($"开始构建......");
			BuildAssetBundleOptions opt = MakeBuildOptions();
			AssetBundleManifest unityManifest = BuildPipeline.BuildAssetBundles(OutputDirectory, buildInfoList.ToArray(), opt, BuildTarget);
			if (unityManifest == null)
				throw new Exception("[BuildPatch] 构建过程中发生错误！");

			// 个人采用AssetBuildInfo.asset作为依赖加载信息文件，不再需要Unity自定义生成的依赖文件
			// 这里采用删除Unity生成的依赖信息文件来避免热更不必要的文件
			// 考虑到.manifest文件最终不会进包，这里保留对应平台的Editor依赖信息方便查看
			var allDependenciesFileName = PathUtilities.GetFolderName(OutputDirectory);
			var allDependenciesFilePath = Path.Combine(OutputDirectory, allDependenciesFileName);
			//var allDependenciesManifestFilePath = Path.Combine(OutputDirectory, $"{allDependenciesFileName}{ResourceConstData.AssetBundleDefaultManifestPostfixWithPoint}");
            File.Delete(allDependenciesFilePath);
			//File.Delete(allDependenciesManifestFilePath);

			// 单独生成包内的AssetBundle的MD5信息(用于热更新判定)
			CreateAssetBundleMd5InfoFile();

            // 视频单独打包
            PackVideo(buildMap);
			//// 加密资源文件
			//List<string> encryptList = EncryptFiles(unityManifest);

			// 1. 检测循环依赖
			CheckCycleDepend(unityManifest);
			// 2. 创建补丁文件
			//CreatePatchManifestFile(unityManifest, buildMap, encryptList);
			// 3. 创建说明文件
			CreateReadmeFile(unityManifest);
            //// 4. 复制更新文件
            //CopyUpdateFiles();

            Log("构建完成！");
		}

        /// <summary>
        /// 更新AssetBundle打包编译信息Asset
        /// </summary>
        /// <param name="buildinfolist"></param>
        /// <param name="abmanifest"></param>
        private void UpdateAssetBundleBuildInfoAsset(List<AssetBundleBuild> buildinfolist, Dictionary<string, AssetBundleBuildInfo> abbuildinfomap)
        {
            // Note: AssetBundle打包信息统一存小写，确保和AB打包那方一致
            var assetbundlebuildinfoassetrelativepath = $"Assets{ResourceConstData.AssetBuildInfoAssetRelativePath}/{ResourceConstData.AssetBuildInfoAssetName}.asset";
            var assetbundlebuildasset = AssetDatabase.LoadAssetAtPath<AssetBuildInfoAsset>(assetbundlebuildinfoassetrelativepath);
            if (assetbundlebuildasset == null)
            {
                assetbundlebuildasset = new AssetBuildInfoAsset();
                AssetDatabase.CreateAsset(assetbundlebuildasset, assetbundlebuildinfoassetrelativepath);
            }
            assetbundlebuildasset.AssetBuildInfoList.Clear();

            // Asset打包信息构建
            foreach (var bi in buildinfolist)
            {
                var abbi = new AssetBuildInfo();
				// 不剔除后缀，确保AssetDatabase模式可以全路径(带后缀)加载
				abbi.AssetPath = bi.assetNames[0].ToLower();//.Substring(0, bi.assetNames[0].Length - Path.GetExtension(bi.assetNames[0]).Length).ToLower();
                abbi.ABPath = bi.assetBundleName.ToLower();
                abbi.ABVariantPath = bi.assetBundleVariant != null ? bi.assetBundleVariant.ToLower() : null;
                assetbundlebuildasset.AssetBuildInfoList.Add(abbi);
            }

            // AssetBundle打包信息构建
            assetbundlebuildasset.AssetBundleBuildInfoList.Clear();
            assetbundlebuildasset.AssetBundleBuildInfoList = abbuildinfomap.Values.ToList();

            EditorUtility.SetDirty(assetbundlebuildasset);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 获取构建选项
        /// </summary>
        private BuildAssetBundleOptions MakeBuildOptions()
		{
			// For the new build system, unity always need BuildAssetBundleOptions.CollectDependencies and BuildAssetBundleOptions.DeterministicAssetBundle
			// 除非设置ForceRebuildAssetBundle标记，否则会进行增量打包

			BuildAssetBundleOptions opt = BuildAssetBundleOptions.None;
			opt |= BuildAssetBundleOptions.StrictMode; //Do not allow the build to succeed if any errors are reporting during it.

			if (CompressOption == ECompressOption.Uncompressed)
				opt |= BuildAssetBundleOptions.UncompressedAssetBundle;
			else if (CompressOption == ECompressOption.ChunkBasedCompressionLZ4)
				opt |= BuildAssetBundleOptions.ChunkBasedCompression;

			if (IsForceRebuild)
				opt |= BuildAssetBundleOptions.ForceRebuildAssetBundle; //Force rebuild the asset bundles
			if (IsAppendHash)
				opt |= BuildAssetBundleOptions.AppendHashToAssetBundleName; //Append the hash to the assetBundle name
			if (IsDisableWriteTypeTree)
				opt |= BuildAssetBundleOptions.DisableWriteTypeTree; //Do not include type information within the asset bundle (don't write type tree).
			if (IsIgnoreTypeTreeChanges)
				opt |= BuildAssetBundleOptions.IgnoreTypeTreeChanges; //Ignore the type tree changes when doing the incremental build check.

			return opt;
		}

		private void Log(string log)
		{
			Debug.Log($"[BuildPatch] {log}");
		}
		private string GetOutputDirectory()
		{
            return $"{_outputRoot}/{BuildTarget}/";
            //{AssetBundleBuildConstData.UnityManifestFileName}";
		}
		private string GetPackageDirectory()
		{
			return $"{_outputRoot}/{BuildTarget}/";
		}

		#region 准备工作
		/// <summary>
		/// 准备工作
		/// </summary>
		private List<AssetInfo> GetBuildMap(ref Dictionary<string, AssetBundleBuildInfo> abbuildinfomap)
		{
			int progressBarCount = 0;
			Dictionary<string, AssetInfo> allAsset = new Dictionary<string, AssetInfo>();

			// 获取所有的收集路径
			List<string> collectDirectorys = AssetBundleCollectSettingData.GetAllCollectDirectory();
			if (collectDirectorys.Count == 0)
				throw new Exception("[BuildPatch] 配置的资源收集路径为空");

			// 获取所有资源
			string[] guids = AssetDatabase.FindAssets(string.Empty, collectDirectorys.ToArray());
			foreach (string guid in guids)
			{
				string mainAssetPath = AssetDatabase.GUIDToAssetPath(guid);
				if (AssetBundleCollectSettingData.IsIgnoreAsset(mainAssetPath))
					continue;
				if (ValidateAsset(mainAssetPath) == false)
					continue;


				List<AssetInfo> depends = GetDependencies(mainAssetPath);
				for (int i = 0; i < depends.Count; i++)
				{
					AssetInfo assetInfo = depends[i];
					if (allAsset.ContainsKey(assetInfo.AssetPath))
					{
						allAsset[assetInfo.AssetPath].DependCount++;
					}
					else
					{
						allAsset.Add(assetInfo.AssetPath, assetInfo);
					}
				}

				// 进度条
				progressBarCount++;
				EditorUtility.DisplayProgressBar("进度", $"依赖文件分析：{progressBarCount}/{guids.Length}", (float)progressBarCount / guids.Length);
			}
			EditorUtility.ClearProgressBar();
			progressBarCount = 0;

			// 移除零依赖的资源
			List<string> removeList = new List<string>();
			foreach (KeyValuePair<string, AssetInfo> pair in allAsset)
			{
				if (pair.Value.IsCollectAsset)
					continue;
				if (pair.Value.DependCount == 0)
					removeList.Add(pair.Value.AssetPath);
			}
			for (int i = 0; i < removeList.Count; i++)
			{
				allAsset.Remove(removeList[i]);
                Debug.Log($"移除零依赖资源:{removeList[i]}");
			}

			// 设置资源标签
			foreach (KeyValuePair<string, AssetInfo> pair in allAsset)
			{
				SetAssetBundleLabelAndVariant(pair.Value);

				// 进度条
				progressBarCount++;
				EditorUtility.DisplayProgressBar("进度", $"设置资源标签：{progressBarCount}/{allAsset.Count}", (float)progressBarCount / allAsset.Count);
			}

            // 整理Asset所有有效的Asset依赖
            // 设置资源标签
            TimeCounter timercounter = new TimeCounter();
            timercounter.Start("AB依赖分析");
            progressBarCount = 0;
            foreach (KeyValuePair<string, AssetInfo> pair in allAsset)
            {
                AssetBundleBuildInfo abbuildinfo = null;
                if (!abbuildinfomap.TryGetValue(pair.Value.AssetBundleLabel, out abbuildinfo))
                {
                    // 统一小写，确保和AssetBuildInfo那方一致
                    var assetbundlelabletolower = pair.Value.AssetBundleLabel.ToLower();
                    abbuildinfo = new AssetBundleBuildInfo(assetbundlelabletolower);
                    abbuildinfomap.Add(pair.Value.AssetBundleLabel, abbuildinfo);
                }
                var directdepends = AssetDatabase.GetDependencies(pair.Key, false);
                foreach(var directdepend in directdepends)
                {
                    AssetInfo assetinfo = null;
                    // allAsset里包含的才是有效的Asset
                    if(allAsset.TryGetValue(directdepend, out assetinfo))
                    {
                        // 统一小写，确保和AssetBuildInfo那方一致
                        var assetablablelower = assetinfo.AssetBundleLabel.ToLower();
                        if (!pair.Value.AssetBundleLabel.Equals(assetinfo.AssetBundleLabel) && !abbuildinfo.DepABPathList.Contains(assetablablelower))
                        {
                            abbuildinfo.DepABPathList.Add(assetablablelower);
                        }
                    }
                }
                // 进度条
                progressBarCount++;
                EditorUtility.DisplayProgressBar("进度", $"整理AB依赖关系：{progressBarCount}/{allAsset.Count}", (float)progressBarCount / allAsset.Count);
            }
            timercounter.End();

            EditorUtility.ClearProgressBar();

			// 返回结果
			return allAsset.Values.ToList();
		}

		/// <summary>
		/// 获取指定资源依赖的资源列表
		/// 注意：返回列表里已经包括主资源自己
		/// </summary>
		private List<AssetInfo> GetDependencies(string assetPath)
		{
			List<AssetInfo> depends = new List<AssetInfo>();
			string[] dependArray = AssetDatabase.GetDependencies(assetPath, true);
			foreach (string dependPath in dependArray)
			{
				if (ValidateAsset(dependPath))
				{
					AssetInfo assetInfo = new AssetInfo(dependPath);
					depends.Add(assetInfo);
				}
			}
			return depends;
		}

        /// <summary>
        /// 检测资源是否有效
        /// </summary>
        private bool ValidateAsset(string assetPath)
		{
			if (!assetPath.StartsWith("Assets/"))
				return false;

			if (AssetDatabase.IsValidFolder(assetPath))
				return false;

			string ext = System.IO.Path.GetExtension(assetPath);
            if (AssetBundleCollectSettingData.Setting.BlackListInfo.PostFixBlackList.Contains(ext))
            {
                return false;
            }
            //if (ext == "" || ext == ".dll" || ext == ".cs" || ext == ".js" || ext == ".boo" || ext == ".meta" || ext == ".tpsheet")
            //	return false;

            string fileName = Path.GetFileName(assetPath);
            if (AssetBundleCollectSettingData.Setting.BlackListInfo.FileNameBlackList.Contains(fileName))
            {
                return false;
            }

            return true;
		}

		/// <summary>
		/// 设置资源的标签和变种
		/// </summary>
		private void SetAssetBundleLabelAndVariant(AssetInfo assetInfo)
		{
			// 如果资源所在文件夹的名称包含后缀符号，则为变体资源
			string folderName = Path.GetDirectoryName(assetInfo.AssetPath); // "Assets/Texture.HD/background.jpg" --> "Assets/Texture.HD"
			if (Path.HasExtension(folderName))
			{
				string extension = Path.GetExtension(folderName);
				string label = AssetBundleCollectSettingData.GetAssetBundleLabel(assetInfo.AssetPath);
				assetInfo.AssetBundleLabel = PathUtilities.GetRegularPath(label.Replace(extension, string.Empty));
				assetInfo.AssetBundleVariant = extension.RemoveFirstChar();
			}
			else
			{
				string label = AssetBundleCollectSettingData.GetAssetBundleLabel(assetInfo.AssetPath);
				assetInfo.AssetBundleLabel = PathUtilities.GetRegularPath(label);
				assetInfo.AssetBundleVariant = ResourceConstData.AssetBundleDefaultVariant;
			}
		}
        #endregion

        #region AssetBundle资源热更新相关
		/// <summary>
		/// 创建AssetBundle的MD5信息文件
		/// </summary>
		private void CreateAssetBundleMd5InfoFile()
        {
			var assetBundleMd5FilePath = AssetBundlePath.GetInnerAssetBundleMd5FilePath();
			// 确保创建最新的
			FileUtilities.DeleteFile(assetBundleMd5FilePath);
            VersionConfigModuleManager.Singleton.initVerisonConfigData();
            var resourceversion = VersionConfigModuleManager.Singleton.InnerGameVersionConfig.ResourceVersionCode;
			// Note: 
			// 这里如果直接指定Encoding.UTF8会出现BOM文件(默认选择了带BOM的方式)
			// 最终导致热更新文件读取比较后的路径信息带了BOM导致https识别时报错导致下载不到正确的资源
            using (var md5SW = new StreamWriter(assetBundleMd5FilePath, false, new UTF8Encoding(false)))
            {
                var abFilesFullPath = Directory.GetFiles(OutputDirectory, "*.*", SearchOption.AllDirectories).Where(f =>
					!f.EndsWith(".meta") && !f.EndsWith(".manifest") && !f.EndsWith("readme.txt")
				);
                var md5hash = MD5.Create();
				// 格式:AB全路径+":"+MD5值
				foreach (var abFilePath in abFilesFullPath)
                {
					var abRelativePath = abFilePath.Remove(0, OutputDirectory.Length);
					abRelativePath = PathUtilities.GetRegularPath(abRelativePath);
					var fileMd5 = FileUtilities.GetFileMD5(abFilePath, md5hash);
					md5SW.WriteLine($"{abRelativePath}{ResourceConstData.AssetBundlleInfoSeparater}{fileMd5}");
                }
                Debug.Log($"AssetBundle的包内MD5信息计算完毕!");
            }
        }
        #endregion

        #region 视频相关
        private void PackVideo(List<AssetInfo> buildMap)
		{
			// 注意：在Unity2018.4截止的版本里，安卓还不支持压缩的视频Bundle
			if (BuildTarget == BuildTarget.Android)
			{
				Log($"开始视频单独打包（安卓平台）");
				for (int i = 0; i < buildMap.Count; i++)
				{
					AssetInfo assetInfo = buildMap[i];
					if (assetInfo.IsVideoAsset)
					{
						BuildAssetBundleOptions opt = BuildAssetBundleOptions.None;
						opt |= BuildAssetBundleOptions.DeterministicAssetBundle;
						opt |= BuildAssetBundleOptions.StrictMode;
						opt |= BuildAssetBundleOptions.UncompressedAssetBundle;
						var videoObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Video.VideoClip>(assetInfo.AssetPath);
						string outPath = OutputDirectory + "/" + assetInfo.AssetBundleLabel.ToLower();
						bool result = BuildPipeline.BuildAssetBundle(videoObj, new[] { videoObj }, outPath, opt, BuildTarget);
						if (result == false)
							throw new Exception($"视频单独打包失败：{assetInfo.AssetPath}");
					}
				}
			}
		}
		#endregion

		#region 文件加密
		
		#endregion

		#region 文件相关
		/// <summary>
		/// 1. 检测循环依赖
		/// </summary>
		private void CheckCycleDepend(AssetBundleManifest unityManifest)
		{
			List<string> visited = new List<string>(100);
			List<string> stack = new List<string>(100);
			string[] allAssetBundles = unityManifest.GetAllAssetBundles();
			for (int i = 0; i < allAssetBundles.Length; i++)
			{
				var element = allAssetBundles[i];
				visited.Clear();
				stack.Clear();

				// 深度优先搜索检测有向图有无环路算法
				if (CheckCycle(unityManifest, element, visited, stack))
				{
					foreach (var ele in stack)
					{
						UnityEngine.Debug.LogWarning(ele);
					}
					throw new Exception($"Found cycle assetbundle : {element}");
				}
			}
		}

        /// <summary>
        /// 检查循环依赖
        /// </summary>
        /// <param name="unityManifest"></param>
        /// <param name="element"></param>
        /// <param name="visited"></param>
        /// <param name="stack"></param>
        /// <returns></returns>
		private bool CheckCycle(AssetBundleManifest unityManifest, string element, List<string> visited, List<string> stack)
		{
			if (visited.Contains(element) == false)
			{
				visited.Add(element);
				stack.Add(element);

				string[] depends = unityManifest.GetDirectDependencies(element);
				foreach (var dp in depends)
				{
					if (visited.Contains(dp) == false && CheckCycle(unityManifest, dp, visited, stack))
						return true;
					else if (stack.Contains(dp))
						return true;
				}
			}

			stack.Remove(element);
			return false;
		}

		/// <summary>
		/// 3. 创建Readme文件到输出目录
		/// </summary>
		private void CreateReadmeFile(AssetBundleManifest unityManifest)
		{
			string[] allAssetBundles = unityManifest.GetAllAssetBundles();

			// 删除旧文件
			string filePath = $"{OutputDirectory}/{AssetBundleBuildConstData.ReadmeFileName}";
			if (File.Exists(filePath))
				File.Delete(filePath);

			Log($"创建说明文件：{filePath}");

			StringBuilder content = new StringBuilder();
			AppendData(content, $"构建平台：{BuildTarget}");
			AppendData(content, $"构建时间：{DateTime.Now}");

			AppendData(content, "");
			AppendData(content, $"--配置信息--");
			for (int i = 0; i < AssetBundleCollectSettingData.Setting.AssetBundleCollectors.Count; i++)
			{
				Collector wrapper = AssetBundleCollectSettingData.Setting.AssetBundleCollectors[i];
                if(wrapper.BuildRule != EAssetBundleBuildRule.ByConstName)
                {
                    AppendData(content, $"Directory : {wrapper.CollectFolderPath} || CollectRule : {wrapper.CollectRule} || BuildRule : {wrapper.BuildRule}");
                }
                else
                {
                    AppendData(content, $"Directory : {wrapper.CollectFolderPath} || CollectRule : {wrapper.CollectRule} || BuildRule : {wrapper.BuildRule} || ConstName : {wrapper.ConstName}");
                }
            }

			AppendData(content, "");
			AppendData(content, $"--构建参数--");
			AppendData(content, $"CompressOption：{CompressOption}");
			AppendData(content, $"ForceRebuild：{IsForceRebuild}");
			AppendData(content, $"DisableWriteTypeTree：{IsDisableWriteTypeTree}");
			AppendData(content, $"IgnoreTypeTreeChanges：{IsIgnoreTypeTreeChanges}");

			AppendData(content, "");
			AppendData(content, $"--构建清单--");
			for (int i = 0; i < allAssetBundles.Length; i++)
			{
				AppendData(content, allAssetBundles[i]);
			}
            
			// 创建新文件
			File.WriteAllText(filePath, content.ToString(), Encoding.UTF8);
		}
		private void AppendData(StringBuilder sb, string data)
		{
			sb.Append(data);
			sb.Append("\r\n");
		}
        #endregion
    }
}