﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web;
using CloneDeploy_Entities.DTOs;
using CloneDeploy_Services.Helpers;
using log4net;

namespace CloneDeploy_Services.Workflows
{
    /// <summary>
    /// Summary description for IsoGen
    /// </summary>
    public class IsoGen
    {
        private readonly ILog log = LogManager.GetLogger("ApplicationLog");

        private const string NewLineChar = "\n";
        private string _userToken { get; set; }
        private readonly string _webPath = Settings.WebPath;
        private readonly IsoGenOptionsDTO _isoOptions;
        private readonly string _configOutPath;
        private readonly string _basePath;
        private readonly string _rootfsPath;
        private readonly string _buildPath;
        private string _outputPath;

        public IsoGen(IsoGenOptionsDTO isoGenOptions)
        {
            _isoOptions = isoGenOptions;
            if (Settings.DebugRequiresLogin == "No" || Settings.OnDemandRequiresLogin == "No" ||
              Settings.RegisterRequiresLogin == "No")
                _userToken = Settings.UniversalToken;
            else
            {
                _userToken = "";
            }
            _basePath = HttpContext.Current.Server.MapPath("~") + Path.DirectorySeparatorChar + "private" +
                       Path.DirectorySeparatorChar;    
            _rootfsPath = _basePath + "client_iso" + Path.DirectorySeparatorChar + "rootfs" + Path.DirectorySeparatorChar;
            _buildPath = _basePath + "client_iso" + Path.DirectorySeparatorChar + "build-tmp";
            _outputPath = _basePath + "client_iso" + Path.DirectorySeparatorChar;
            _configOutPath = _basePath + "client_iso" + Path.DirectorySeparatorChar + "config" +
                            Path.DirectorySeparatorChar;
        }

        public bool Generate()
        {
            try
            {
                if (Directory.Exists(_configOutPath))
                    Directory.Delete(_configOutPath, true);
                Directory.CreateDirectory(_configOutPath);
                Directory.CreateDirectory(_configOutPath + "clonedeploy");
                Directory.CreateDirectory(_configOutPath + "EFI");
                Directory.CreateDirectory(_configOutPath + "EFI" + Path.DirectorySeparatorChar + "boot");
                Directory.CreateDirectory(_configOutPath + "syslinux");
                File.Copy(Settings.TftpPath + "images" + Path.DirectorySeparatorChar + _isoOptions.bootImage,
                    _configOutPath + "clonedeploy" + Path.DirectorySeparatorChar + _isoOptions.bootImage, true);
                File.Copy(Settings.TftpPath + "kernels" + Path.DirectorySeparatorChar + _isoOptions.kernel,
                    _configOutPath + "clonedeploy" + Path.DirectorySeparatorChar + _isoOptions.kernel, true);
            }
            catch (Exception ex)
            {
                log.Debug(ex.Message);
                return false;
            }

            CreateSyslinuxMenu();
            CreateGrubMenu();

            if (_isoOptions.buildType == "ISO")
            {
                StartMkIsofs();
            }
            else
            {
                CreateUsb();
            }


            return true;
        }



        private void CreateSyslinuxMenu()
        {
            var sysLinuxMenu = new StringBuilder();

            sysLinuxMenu.Append("DEFAULT vesamenu.c32" + NewLineChar);
            sysLinuxMenu.Append("MENU TITLE Boot Menu" + NewLineChar);
            sysLinuxMenu.Append("MENU BACKGROUND bg.png" + NewLineChar);
            sysLinuxMenu.Append("menu tabmsgrow 22" + NewLineChar);
            sysLinuxMenu.Append("menu cmdlinerow 22" + NewLineChar);
            sysLinuxMenu.Append("menu endrow 24" + NewLineChar);
            sysLinuxMenu.Append("menu color title                1;34;49    #eea0a0ff #cc333355 std" + NewLineChar);
            sysLinuxMenu.Append("menu color sel                  7;37;40    #ff000000 #bb9999aa all" + NewLineChar);
            sysLinuxMenu.Append("menu color border               30;44      #ffffffff #00000000 std" + NewLineChar);
            sysLinuxMenu.Append("menu color pwdheader            31;47      #eeff1010 #20ffffff std" + NewLineChar);
            sysLinuxMenu.Append("menu color hotkey               35;40      #90ffff00 #00000000 std" + NewLineChar);
            sysLinuxMenu.Append("menu color hotsel               35;40      #90000000 #bb9999aa all" + NewLineChar);
            sysLinuxMenu.Append("menu color timeout_msg          35;40      #90ffffff #00000000 none" + NewLineChar);
            sysLinuxMenu.Append("menu color timeout              31;47      #eeff1010 #00000000 none" + NewLineChar);
            sysLinuxMenu.Append("NOESCAPE 0" + NewLineChar);
            sysLinuxMenu.Append("ALLOWOPTIONS 0" + NewLineChar);
            sysLinuxMenu.Append("" + NewLineChar);
            sysLinuxMenu.Append("LABEL local" + NewLineChar);
            sysLinuxMenu.Append("localboot 0" + NewLineChar);
            sysLinuxMenu.Append("MENU DEFAULT" + NewLineChar);
            sysLinuxMenu.Append("MENU LABEL Boot To Local Machine" + NewLineChar);
            sysLinuxMenu.Append("" + NewLineChar);

            sysLinuxMenu.Append("LABEL Download Image" + NewLineChar);
            sysLinuxMenu.Append("kernel /clonedeploy/" + _isoOptions.kernel + "" + NewLineChar);
            sysLinuxMenu.Append("append initrd=/clonedeploy/" + _isoOptions.bootImage +
                                " root=/dev/ram0 rw ramdisk_size=156000 " + " web=" + _webPath + " USER_TOKEN=" +
                                _userToken +
                                " task=push consoleblank=0 " + _isoOptions.arguments + "" + NewLineChar);
            sysLinuxMenu.Append("MENU LABEL Download Image" + NewLineChar);
            sysLinuxMenu.Append("" + NewLineChar);

            sysLinuxMenu.Append("LABEL Upload Image" + NewLineChar);
            sysLinuxMenu.Append("kernel /clonedeploy/" + _isoOptions.kernel + "" + NewLineChar);
            sysLinuxMenu.Append("append initrd=/clonedeploy/" + _isoOptions.bootImage +
                                " root=/dev/ram0 rw ramdisk_size=156000 " + " web=" + _webPath + " USER_TOKEN=" +
                                _userToken +
                                " task=pull consoleblank=0 " + _isoOptions.arguments + "" + NewLineChar);
            sysLinuxMenu.Append("MENU LABEL Upload Image" + NewLineChar);
            sysLinuxMenu.Append("" + NewLineChar);

            sysLinuxMenu.Append("LABEL Client Console" + NewLineChar);
            sysLinuxMenu.Append("kernel /clonedeploy/" + _isoOptions.kernel + "" + NewLineChar);
            sysLinuxMenu.Append("append initrd=/clonedeploy/" + _isoOptions.bootImage +
                                " root=/dev/ram0 rw ramdisk_size=156000 " + " web=" + _webPath + " USER_TOKEN=" +
                                _userToken +
                                " task=debug consoleblank=0 " + _isoOptions.arguments + "" + NewLineChar);
            sysLinuxMenu.Append("MENU LABEL Client Console" + NewLineChar);
            sysLinuxMenu.Append("" + NewLineChar);

            sysLinuxMenu.Append("LABEL Add Computer" + NewLineChar);
            sysLinuxMenu.Append("kernel /clonedeploy/" + _isoOptions.kernel + "" + NewLineChar);
            sysLinuxMenu.Append("append initrd=/clonedeploy/" + _isoOptions.bootImage +
                                " root=/dev/ram0 rw ramdisk_size=156000 " + " web=" + _webPath + " USER_TOKEN=" +
                                _userToken +
                                " task=register consoleblank=0 " + _isoOptions.arguments + "" + NewLineChar);
            sysLinuxMenu.Append("MENU LABEL Add Computer" + NewLineChar);
            sysLinuxMenu.Append("" + NewLineChar);

            sysLinuxMenu.Append("LABEL On Demand" + NewLineChar);
            sysLinuxMenu.Append("kernel /clonedeploy/" + _isoOptions.kernel + "" + NewLineChar);
            sysLinuxMenu.Append("append initrd=/clonedeploy/" + _isoOptions.bootImage +
                                " root=/dev/ram0 rw ramdisk_size=156000 " + " web=" + _webPath + " USER_TOKEN=" +
                                _userToken +
                                " task=ond consoleblank=0 " + _isoOptions.arguments + "" + NewLineChar);
            sysLinuxMenu.Append("MENU LABEL On Demand" + NewLineChar);
            sysLinuxMenu.Append("" + NewLineChar);

            sysLinuxMenu.Append("LABEL Diagnostics" + NewLineChar);
            sysLinuxMenu.Append("kernel /clonedeploy/" + _isoOptions.kernel + "" + NewLineChar);
            sysLinuxMenu.Append("append initrd=/clonedeploy/" + _isoOptions.bootImage +
                                " root=/dev/ram0 rw ramdisk_size=156000 " + " web=" + _webPath + " USER_TOKEN=" +
                                _userToken +
                                " task=diag consoleblank=0 " + _isoOptions.arguments + "" + NewLineChar);
            sysLinuxMenu.Append("MENU LABEL Diagnostics" + NewLineChar);
            sysLinuxMenu.Append("" + NewLineChar);

            sysLinuxMenu.Append("PROMPT 0" + NewLineChar);
            sysLinuxMenu.Append("TIMEOUT 0" + NewLineChar);

            string outFile;
            if (_isoOptions.buildType == "ISO")
                outFile = _configOutPath + "syslinux" + Path.DirectorySeparatorChar + "isolinux.cfg";
            else
                outFile = _configOutPath + "syslinux" + Path.DirectorySeparatorChar + "syslinux.cfg";

            new FileOps().WritePath(outFile, sysLinuxMenu.ToString());
           
        }

        private void CreateGrubMenu()
        {
            var grubMenu = new StringBuilder();

            grubMenu.Append("# Global options" + NewLineChar);
            grubMenu.Append("set timeout=-1" + NewLineChar);
            grubMenu.Append("set default=0" + NewLineChar);
            grubMenu.Append("set fallback=1" + NewLineChar);
            grubMenu.Append("set pager=1" + NewLineChar);

            grubMenu.Append("if loadfont /EFI/boot/unicode.pf2; then" + NewLineChar);
            grubMenu.Append("set gfxmode=1024x768,auto" + NewLineChar);
            grubMenu.Append("insmod efi_gop" + NewLineChar);
            grubMenu.Append("insmod efi_uga" + NewLineChar);
            grubMenu.Append("insmod gfxterm" + NewLineChar);
            grubMenu.Append("terminal_output gfxterm" + NewLineChar);
            grubMenu.Append("fi" + NewLineChar);
           
            grubMenu.Append("" + NewLineChar);
            grubMenu.Append("menuentry \"Boot To Local Machine\" {" + NewLineChar);
            grubMenu.Append("exit" + NewLineChar);
            grubMenu.Append("}" + NewLineChar);
            grubMenu.Append("" + NewLineChar);

            grubMenu.Append("menuentry \"Download Image\" {" + NewLineChar);       
	        grubMenu.Append("set gfxpayload=keep" + NewLineChar);
            grubMenu.Append("linux	/clonedeploy/" + _isoOptions.kernel + " ramdisk_size=156000 root=/dev/ram0 rw web=" + _webPath +
                " USER_TOKEN=" + _userToken + " task=push consoleblank=0 " + _isoOptions.arguments + NewLineChar);
            grubMenu.Append("initrd	/clonedeploy/" + _isoOptions.bootImage + NewLineChar);
            grubMenu.Append("}" + NewLineChar);
            grubMenu.Append("" + NewLineChar);

            grubMenu.Append("menuentry \"Upload Image\" {" + NewLineChar);
            grubMenu.Append("set gfxpayload=keep" + NewLineChar);
            grubMenu.Append("linux	/clonedeploy/" + _isoOptions.kernel + " ramdisk_size=156000 root=/dev/ram0 rw web=" + _webPath +
                " USER_TOKEN=" + _userToken + " task=pull consoleblank=0 " + _isoOptions.arguments + NewLineChar);
            grubMenu.Append("initrd	/clonedeploy/" + _isoOptions.bootImage + NewLineChar);
            grubMenu.Append("}" + NewLineChar);
            grubMenu.Append("" + NewLineChar);

            grubMenu.Append("menuentry \"Client Console\" {" + NewLineChar);
            grubMenu.Append("set gfxpayload=keep" + NewLineChar);
            grubMenu.Append("linux	/clonedeploy/" + _isoOptions.kernel + " ramdisk_size=156000 root=/dev/ram0 rw web=" + _webPath +
                " USER_TOKEN=" + _userToken + " task=debug consoleblank=0 " + _isoOptions.arguments + NewLineChar);
            grubMenu.Append("initrd	/clonedeploy/" + _isoOptions.bootImage + NewLineChar);
            grubMenu.Append("}" + NewLineChar);
            grubMenu.Append("" + NewLineChar);

            grubMenu.Append("menuentry \"Add Computer\" {" + NewLineChar);
            grubMenu.Append("set gfxpayload=keep" + NewLineChar);
            grubMenu.Append("linux	/clonedeploy/" + _isoOptions.kernel + " ramdisk_size=156000 root=/dev/ram0 rw web=" + _webPath +
                " USER_TOKEN=" + _userToken + " task=register consoleblank=0 " + _isoOptions.arguments + NewLineChar);
            grubMenu.Append("initrd	/clonedeploy/" + _isoOptions.bootImage + NewLineChar);
            grubMenu.Append("}" + NewLineChar);
            grubMenu.Append("" + NewLineChar);

            grubMenu.Append("menuentry \"On Demand\" {" + NewLineChar);
            grubMenu.Append("set gfxpayload=keep" + NewLineChar);
            grubMenu.Append("linux	/clonedeploy/" + _isoOptions.kernel + " ramdisk_size=156000 root=/dev/ram0 rw web=" + _webPath +
                " USER_TOKEN=" + _userToken + " task=ond consoleblank=0 " + _isoOptions.arguments + NewLineChar);
            grubMenu.Append("initrd	/clonedeploy/" + _isoOptions.bootImage + NewLineChar);
            grubMenu.Append("}" + NewLineChar);
            grubMenu.Append("" + NewLineChar);

            grubMenu.Append("menuentry \"Diagnostics\" {" + NewLineChar);
            grubMenu.Append("set gfxpayload=keep" + NewLineChar);
            grubMenu.Append("linux	/clonedeploy/" + _isoOptions.kernel + " ramdisk_size=156000 root=/dev/ram0 rw web=" + _webPath +
                " USER_TOKEN=" + _userToken + " task=diag consoleblank=0 " + _isoOptions.arguments + NewLineChar);
            grubMenu.Append("initrd	/clonedeploy/" + _isoOptions.bootImage + NewLineChar);
            grubMenu.Append("}" + NewLineChar);
            grubMenu.Append("" + NewLineChar);


            var outFile = _configOutPath + "EFI" + Path.DirectorySeparatorChar + "boot" + Path.DirectorySeparatorChar +
                          "grub.cfg";

            new FileOps().WritePath(outFile, grubMenu.ToString());
        }

        private string GenerateArgs()
        {
            var logPath = _basePath + "logs" + Path.DirectorySeparatorChar + "mkisofs.log";

            string arguments;
            var isUnix = Environment.OSVersion.ToString().Contains("Unix");
            if (isUnix)
            {
                arguments = "-c \"mkisofs -o " + "\"" + _outputPath + "\"" +
                            " -log-file \"" + logPath + "\"" +
                            " -graft-points -joliet -R -full-iso9660-filenames -allow-lowercase" +
                            " -b syslinux/isolinux.bin -c syslinux/boot.cat -no-emul-boot -boot-load-size 4 -boot-info-table" +
                            " -eltorito-alt-boot -e EFI/images/efiboot.img -no-emul-boot " + _buildPath + "\"";
            }
            else
            {
                var appPath = _basePath + "apps" + Path.DirectorySeparatorChar + "mkisofs.exe";
                arguments = "/c \"cd /d " + _buildPath + " & " + " \"" + appPath + "\"" + " -o " + "\"" + _outputPath + "\"" +
                              " -log-file \"" + logPath + "\"" +
                              " -graft-points -joliet -R -full-iso9660-filenames -allow-lowercase" +
                              " -b syslinux/isolinux.bin -c syslinux/boot.cat -no-emul-boot -boot-load-size 4 -boot-info-table" +
                              " -eltorito-alt-boot -eltorito-platform 0xEF -eltorito-boot EFI/images/efiboot.img -no-emul-boot . " + "\"";
            }
            return arguments;
        }

        private void CleanBuildPath()
        {
            if (Directory.Exists(_buildPath))
            {
                Directory.Delete(_buildPath, true);
            }
        }
        private bool CreateUsb()
        {
            _outputPath += "clientboot.zip";

            //copy base root path to temporary location
            new FileOps().Copy(_rootfsPath, _buildPath);
            //copy newly generated config files on top of temporary location
            new FileOps().Copy(_configOutPath, _buildPath);

            new Zip().Create(_outputPath,_buildPath);

            CleanBuildPath();

            return true;
        }

        private bool StartMkIsofs()
        {
            _outputPath += "clientboot.iso";

            //copy base root path to temporary location
            new FileOps().Copy(_rootfsPath, _buildPath);
            //copy newly generated config files on top of temporary location
            new FileOps().Copy(_configOutPath, _buildPath);


            var isUnix = Environment.OSVersion.ToString().Contains("Unix");
            var shell = isUnix ? "/bin/bash" : "cmd.exe";

            var processArguments = GenerateArgs();
            if (processArguments == null) return false;
            var pInfo = new ProcessStartInfo { FileName = shell, Arguments = processArguments };

            Process makeIso;
            try
            {
                makeIso = Process.Start(pInfo);
            }
            catch (Exception ex)
            {

                log.Debug(ex.ToString());
                return false;
            }

            if (makeIso == null)
            {
                CleanBuildPath();
                return false;
            }

            makeIso.WaitForExit(15000);
            
            CleanBuildPath();
            return true;
        }
    }
}