﻿using Infrastructure;
using Infrastructure.Attribute;
using Microsoft.AspNetCore.Http;
using UAParser;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.System.Dto;
using ZR.Repository;
using ZR.ServiceCore.Model.Dto;

namespace ZR.ServiceCore.Services
{
    /// <summary>
    /// 登录
    /// </summary>
    [AppService(ServiceType = typeof(ISysLoginService), ServiceLifetime = LifeTime.Transient)]
    public class SysLoginService : BaseService<SysLogininfor>, ISysLoginService
    {
        private readonly ISysUserService SysUserService;
        private readonly IHttpContextAccessor httpContextAccessor;

        public SysLoginService(ISysUserService sysUserService, IHttpContextAccessor httpContextAccessor)
        {
            SysUserService = sysUserService;
            this.httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// 登录验证
        /// </summary>
        /// <param name="logininfor"></param>
        /// <param name="loginBody"></param>
        /// <returns></returns>
        public SysUser Login(LoginBodyDto loginBody, SysLogininfor logininfor)
        {
            if (loginBody.Password.Length != 32)
            {
                loginBody.Password = NETCore.Encrypt.EncryptProvider.Md5(loginBody.Password);
            }
            SysUser user = SysUserService.Login(loginBody);
            logininfor.UserName = loginBody.Username;
            logininfor.Status = "1";
            logininfor.LoginTime = DateTime.Now;
            logininfor.Ipaddr = loginBody.LoginIP;

            ClientInfo clientInfo = httpContextAccessor.HttpContext.GetClientInfo();
            logininfor.Browser = clientInfo.ToString();
            logininfor.Os = clientInfo.OS.ToString();

            if (user == null || user.UserId <= 0)
            {
                logininfor.Msg = "用户名或密码错误";
                AddLoginInfo(logininfor);
                throw new CustomException(ResultCode.LOGIN_ERROR, logininfor.Msg, false);
            }
            if (user.Status == 1)
            {
                logininfor.Msg = "该用户已禁用";
                AddLoginInfo(logininfor);
                throw new CustomException(ResultCode.LOGIN_ERROR, logininfor.Msg, false);
            }

            logininfor.Status = "0";
            logininfor.Msg = "登录成功";
            AddLoginInfo(logininfor);
            SysUserService.UpdateLoginInfo(loginBody.LoginIP, user.UserId);
            return user;
        }
        /// <summary>
        /// 登录验证
        /// </summary>
        /// <param name="logininfor"></param>
        /// <param name="loginBody"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public SysUser PhoneLogin(PhoneLoginDto loginBody, SysLogininfor logininfor, SysUser user)
        {
            logininfor.UserName = user.UserName;
            logininfor.Status = "1";
            logininfor.LoginTime = DateTime.Now;
            logininfor.Ipaddr = loginBody.LoginIP;

            ClientInfo clientInfo = httpContextAccessor.HttpContext.GetClientInfo();
            logininfor.Browser = clientInfo.ToString();
            logininfor.Os = clientInfo.OS.ToString();

            if (user.Status == 1)
            {
                logininfor.Msg = "该用户已禁用";
                AddLoginInfo(logininfor);
                throw new CustomException(ResultCode.LOGIN_ERROR, logininfor.Msg, false);
            }

            logininfor.Status = "0";
            logininfor.Msg = "登录成功";
            AddLoginInfo(logininfor);
            SysUserService.UpdateLoginInfo(loginBody.LoginIP, user.UserId);
            return user;
        }
        /// <summary>
        /// 查询登录日志
        /// </summary>
        /// <param name="logininfoDto"></param>
        /// <param name="pager">分页</param>
        /// <returns></returns>
        public PagedInfo<SysLogininfor> GetLoginLog(SysLogininfor logininfoDto, PagerInfo pager)
        {
            //logininfoDto.BeginTime = DateTimeHelper.GetBeginTime(logininfoDto.BeginTime, -1);
            //logininfoDto.EndTime = DateTimeHelper.GetBeginTime(logininfoDto.EndTime, 1);

            var exp = Expressionable.Create<SysLogininfor>();

            exp.AndIF(logininfoDto.BeginTime == null, it => it.LoginTime >= DateTime.Now.ToShortDateString().ParseToDateTime());
            exp.AndIF(logininfoDto.BeginTime != null, it => it.LoginTime >= logininfoDto.BeginTime && it.LoginTime <= logininfoDto.EndTime);
            exp.AndIF(logininfoDto.Ipaddr.IfNotEmpty(), f => f.Ipaddr == logininfoDto.Ipaddr);
            exp.AndIF(logininfoDto.UserName.IfNotEmpty(), f => f.UserName.Contains(logininfoDto.UserName));
            exp.AndIF(logininfoDto.Status.IfNotEmpty(), f => f.Status == logininfoDto.Status);
            var query = Queryable().Where(exp.ToExpression())
            .OrderBy(it => it.InfoId, OrderByType.Desc);

            return query.ToPage(pager);
        }

        /// <summary>
        /// 记录登录日志
        /// </summary>
        /// <param name="sysLogininfor"></param>
        /// <returns></returns>
        public void AddLoginInfo(SysLogininfor sysLogininfor)
        {
            Insert(sysLogininfor);
        }

        /// <summary>
        /// 清空登录日志
        /// </summary>
        public void TruncateLogininfo()
        {
            Truncate();
        }

        /// <summary>
        /// 删除登录日志
        /// </summary>
        /// <param name="ids"></param>
        /// <returns></returns>
        public int DeleteLogininforByIds(long[] ids)
        {
            return Delete(ids);
        }

        public void CheckLockUser(string userName)
        {
            var lockTimeStamp = CacheService.GetLockUser(userName);
            var lockTime = DateTimeHelper.ToLocalTimeDateBySeconds(lockTimeStamp);
            var ts = lockTime - DateTime.Now;

            if (lockTimeStamp > 0 && ts.TotalSeconds > 0)
            {
                throw new CustomException(ResultCode.LOGIN_ERROR, $"你的账号已被锁,剩余{Math.Round(ts.TotalMinutes, 0)}分钟");
            }
        }

    }
}
