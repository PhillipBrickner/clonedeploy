﻿using System.Collections.Generic;
using System.IO;

using CloneDeploy_Entities;
using CloneDeploy_Entities.DTOs;
using RestSharp;

namespace CloneDeploy_ApiCalls
{
    public class FilesystemAPI: BaseAPI
    {
      

        public FilesystemAPI(string resource):base(resource)
        {
         
        }

       

        public bool BootSdiExists()
        {
            _request.Method = Method.GET;
            _request.Resource = string.Format("api/{0}/BootSdiExists/", _resource);
            var response = new ApiRequest().Execute<ApiBoolResponseDTO>(_request).Value;

            return response;
        }

        public string ReadFileText(string path)
        {
            _request.Method = Method.GET;
            _request.Resource = string.Format("api/{0}/ReadFileText/", _resource);
            _request.AddParameter("path", path);
            var response = new ApiRequest().Execute<ApiStringResponseDTO>(_request).Value;

            return response;

        }

        public bool SetUnixPermissions(string path)
        {
            _request.Method = Method.GET;
            _request.Resource = string.Format("api/{0}/SetUnixPermissions/", _resource);
            _request.AddParameter("path", path);
            var response = new ApiRequest().Execute<ApiBoolResponseDTO>(_request).Value;

            return response;

        }

        public List<string> GetKernels()
        {
            _request.Method = Method.GET;
            _request.Resource = string.Format("api/{0}/GetKernels/", _resource);
            var response = new ApiRequest().Execute<List<string>>(_request);

            return response;

        }

        public List<string> GetBootImages()
        {
            _request.Method = Method.GET;
            _request.Resource = string.Format("api/{0}/GetBootImages/", _resource);
            var response = new ApiRequest().Execute<List<string>>(_request);

            return response;

        }

        public List<string> GetLogs()
        {
            _request.Method = Method.GET;
            _request.Resource = string.Format("api/{0}/GetLogs/", _resource);
            var response = new ApiRequest().Execute<List<string>>(_request);

            return response;

        }

        public List<string> GetScripts(string type)
        {
            _request.Method = Method.GET;
            _request.Resource = string.Format("api/{0}/GetScripts/", _resource);
            _request.AddParameter("type", type);
            var response = new ApiRequest().Execute<List<string>>(_request);

            return response;

        }

        public List<string> GetThinImages()
        {
            _request.Method = Method.GET;
            _request.Resource = string.Format("api/{0}/GetThinImages/", _resource);
            var response = new ApiRequest().Execute<List<string>>(_request);

            return response;

        }


        public MunkiPackageInfoEntity GetPlist(string file)
        {
            _request.Method = Method.GET;
            _request.Resource = string.Format("api/{0}/GetPlist/", _resource);
            _request.AddParameter("file", file);
            var response = new ApiRequest().Execute<MunkiPackageInfoEntity>(_request);

            return response;
        }


        public List<FileInfo> GetMunkiResources(string resourceType)
        {
            _request.Method = Method.GET;
            _request.Resource = string.Format("api/{0}/GetMunkiResources/", _resource);
            _request.AddParameter("resourceType", resourceType);
            var response = new ApiRequest().Execute<List<FileInfo>>(_request);

            return response;
        }

        public DpFreeSpaceDTO GetDpFreeSpace()
        {
            _request.Method = Method.GET;
            _request.Resource = string.Format("api/{0}/GetDpFreeSpace/", _resource);
            var response = new ApiRequest().Execute<DpFreeSpaceDTO>(_request);

            return response;
        }

        public string GetDefaultBootFilePath(string type)
        {
            _request.Method = Method.GET;
            _request.Resource = string.Format("api/{0}/GetDefaultBootFilePath/", _resource);
            _request.AddParameter("type", type);
            var response = new ApiRequest().Execute<ApiStringResponseDTO>(_request).Value;

            return response;
        }

        public bool EditDefaultBootMenu(CoreScriptDTO menu)
        {
            _request.Method = Method.POST;
            _request.Resource = string.Format("api/{0}/EditDefaultBootMenu/", _resource);
            _request.AddJsonBody(menu);
            var response = new ApiRequest().Execute<ApiBoolResponseDTO>(_request).Value;

            return response;
        }

        public bool WriteCoreScript(CoreScriptDTO script)
        {
            _request.Method = Method.POST;
            _request.Resource = string.Format("api/{0}/WriteCoreScript/", _resource);
            _request.AddJsonBody(script);
            var response = new ApiRequest().Execute<ApiBoolResponseDTO>(_request).Value;

            return response;
        }

        public string GetServerPaths(string type, string subType)
        {
            _request.Method = Method.GET;
            _request.Resource = string.Format("api/{0}/GetServerPaths/", _resource);
            _request.AddParameter("type", type);
            _request.AddParameter("subType", subType);
            var response = new ApiRequest().Execute<ApiStringResponseDTO>(_request).Value;

            return response;
        }

        public IEnumerable<string> GetLogContents(string name,int limit)
        {
            _request.Method = Method.GET;
            _request.Resource = string.Format("api/{0}/GetLogContents/", _resource);
            _request.AddParameter("name", name);
            _request.AddParameter("limit", limit);
            var response = new ApiRequest().Execute<List<string>>(_request);

            return response;
        }

      
    }
}