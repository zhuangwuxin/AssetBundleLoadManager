﻿/**
 * Auto generated by XbufferExcelToData, do not edit it 
 * 表格名字
 */
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using xbuffer;

namespace Data
{
    public class t_language_cnContainer
    {
        private List<t_language_cn> list = null;
        private Dictionary<string, t_language_cn> map = null;

        public List<t_language_cn> getList()
        {
            if (list == null || list.Count <= 0)
                loadDataFromBin();
            return list;
        }

        public Dictionary<string, t_language_cn> getMap()
        {
            if (map == null || map.Count <= 0)
                loadDataFromBin();
            return map;
        }

        public void ClearList()
        {
            if (list != null && list.Count > 0)
                list.Clear();
            if (map != null && map.Count > 0)
                map.Clear();
        }   

        public void loadDataFromBin()
        {   
            Stream fs = ConfLoader.Singleton.getStreamByteName(typeof(t_language_cn).Name);
            if(fs != null)
            {
                BinaryReader br = new BinaryReader(fs);
                uint offset = 0;
                bool frist = true;
                try{
                    while (fs.Length - fs.Position > 0)
                    {
                        if (frist)
                        {
                            frist = false;
                            ClearList();
                            var count = br.ReadInt32();
                            list =  new List<t_language_cn>(count);
                            map = new Dictionary<string, t_language_cn>(count);
                        }

                        var length = br.ReadInt32();
                        var data = br.ReadBytes(length);
                        var obj= t_language_cnBuffer.deserialize(data, ref offset);
                        offset = 0;
                        list.Add(obj);
                        map.Add(obj.Key, obj); 
                    }
                }catch (Exception ex)
                {
                    Debug.LogError("import data error: " + ex.ToString());
                }           
                br.Close();
                fs.Close();
            }
        }
    }
}