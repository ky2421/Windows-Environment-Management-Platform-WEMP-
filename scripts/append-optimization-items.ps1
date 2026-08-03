$ErrorActionPreference = 'Stop'
$path = 'D:\WEMP\database\seed\optimization-items.json'

$json = Get-Content $path -Raw -Encoding UTF8
$kb = $json | ConvertFrom-Json

$new = @(
    # ============ 一、服务类（10 条，lfsvc 已在库中不重复） ============
    @{ code = 'svc.pca-svc'; category = 'service'; name = '禁用程序兼容性助手（PcaSvc）';
       principle = '旧软件兼容性检测助手，后台检测并提示兼容性问题；日常只使用新版软件的机器纯属后台占用。';
       risk = '运行十几年老程序时可能失去兼容性提示与自动修复。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"serviceName":"PcaSvc"}'; enabled = $true; sortOrder = 180 },
    @{ code = 'svc.trk-wks'; category = 'service'; name = '禁用分布式链接跟踪客户端（TrkWks）';
       principle = '企业局域网内追踪 NTFS 文件的快捷方式；家用单机完全无用。';
       risk = '局域网共享文件的快捷方式可能失效；单机无影响。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"serviceName":"TrkWks"}'; enabled = $true; sortOrder = 190 },
    @{ code = 'svc.scard-svr'; category = 'service'; name = '禁用智能卡服务（ScardSvr）';
       principle = '实体智能 IC 卡读卡器验证服务；普通家用电脑没有读卡器，纯属后台占用。';
       risk = 'USB 智能卡读卡器无法使用。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"serviceName":"ScardSvr"}'; enabled = $true; sortOrder = 200 },
    @{ code = 'svc.shared-access'; category = 'service'; name = '禁用 Internet 连接共享（SharedAccess）';
       principle = '电脑开启 WiFi 热点共享网络的服务；不用电脑开热点的机器可禁用。';
       risk = '无法用电脑开热点分享网络（移动热点功能失效）。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"serviceName":"SharedAccess"}'; enabled = $true; sortOrder = 210 },
    @{ code = 'svc.wallet'; category = 'service'; name = '禁用系统钱包支付服务（WalletService）';
       principle = '系统钱包支付服务，国内几乎无人使用，纯属后台占用。';
       risk = 'Windows 钱包类功能不可用。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"serviceName":"WalletService"}'; enabled = $true; sortOrder = 220 },
    @{ code = 'svc.wbio'; category = 'service'; name = '禁用生物识别服务（WbioSrvc）';
       principle = '指纹、Windows Hello 人脸识别服务；台式机无摄像头/指纹设备可禁用。';
       risk = '笔记本带人脸/指纹识别会失效，此类设备请勿禁用。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"serviceName":"WbioSrvc"}'; enabled = $true; sortOrder = 230 },
    @{ code = 'svc.wisvc'; category = 'service'; name = '禁用 Windows 预览体验计划服务（wisvc）';
       principle = 'Windows Insider 预览体验计划服务；没有加入测试版系统的机器纯属占用。';
       risk = '无法接收预览版系统更新。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"serviceName":"wisvc"}'; enabled = $true; sortOrder = 240 },
    @{ code = 'svc.dmwappush'; category = 'service'; name = '禁用遥测推送服务（Dmwappushservice）';
       principle = '遥测推送后台服务，收集使用数据并推送；禁用减少隐私泄露与后台占用。';
       risk = '极低；个别推送类系统功能可能失效。';
       recommendation = 'required'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"serviceName":"Dmwappushservice"}'; enabled = $true; sortOrder = 250 },
    @{ code = 'svc.wmp-network'; category = 'service'; name = '禁用 WMP 媒体库网络共享（WMPNetworkSvc）';
       principle = 'Windows 媒体播放器媒体库网络共享服务；不用 WMP 局域网投屏共享可禁用。';
       risk = '无法通过 WMP 与其他设备共享媒体库。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"serviceName":"WMPNetworkSvc"}'; enabled = $true; sortOrder = 260 },
    @{ code = 'svc.cert-prop'; category = 'service'; name = '禁用证书传播服务（CertPropSvc）';
       principle = '企业域环境智能卡证书同步服务；家用电脑无作用。';
       risk = '域环境智能卡证书无法同步；家用无影响。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"serviceName":"CertPropSvc"}'; enabled = $true; sortOrder = 270 },
    # ============ Edge 更新服务（改为手动，不禁用） ============
    @{ code = 'svc.edge-update-manual'; category = 'service'; name = 'Edge 更新服务改为手动';
       principle = 'Edge 浏览器后台自动更新服务；不频繁使用 Edge 可改为手动启动，减少后台常驻，不要直接禁用（防止浏览器异常）。';
       risk = 'Edge 需手动触发更新或打开浏览器时才会检查更新。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"services":["edgeupdate","edgeupdatem"],"startMode":"manual"}'; enabled = $true; sortOrder = 280 },
    # ============ 二、计划任务类（9 条） ============
    @{ code = 'task.compat-appraiser'; category = 'scheduled-task'; name = '禁用兼容性数据收集任务（Appraiser）';
       principle = '后台收集软件兼容性数据，容易导致磁盘瞬间占用 100%。';
       risk = '失去兼容性遥测数据，无日常影响。';
       recommendation = 'required'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"tasks":["\\Microsoft\\Windows\\Application Experience\\Microsoft Compatibility Appraiser"]}'; enabled = $true; sortOrder = 10 },
    @{ code = 'task.programdata-updater'; category = 'scheduled-task'; name = '禁用 ProgramDataUpdater 数据上报任务';
       principle = '配合兼容性评估后台上报使用数据。';
       risk = '无日常影响。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"tasks":["\\Microsoft\\Windows\\Application Experience\\ProgramDataUpdater"]}'; enabled = $true; sortOrder = 20 },
    @{ code = 'task.ceip-consolidator'; category = 'scheduled-task'; name = '禁用 CEIP 用户体验数据汇总任务';
       principle = '微软用户体验改善计划（CEIP）数据汇总上报任务。';
       risk = '无日常影响。';
       recommendation = 'required'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"tasks":["\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator"]}'; enabled = $true; sortOrder = 30 },
    @{ code = 'task.ceip-usbceip'; category = 'scheduled-task'; name = '禁用 USB 使用体验上报任务（UsbCeip）';
       principle = '上报 USB 设备使用情况给微软。';
       risk = '无日常影响。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"tasks":["\\Microsoft\\Windows\\Customer Experience Improvement Program\\UsbCeip"]}'; enabled = $true; sortOrder = 40 },
    @{ code = 'task.ceip-kernelceip'; category = 'scheduled-task'; name = '禁用内核事件体验上报任务（KernelCeipTask）';
       principle = '上报内核事件给微软。';
       risk = '无日常影响。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"tasks":["\\Microsoft\\Windows\\Customer Experience Improvement Program\\KernelCeipTask"]}'; enabled = $true; sortOrder = 50 },
    @{ code = 'task.feedback'; category = 'scheduled-task'; name = '禁用反馈中心任务（问卷收集）';
       principle = '反馈中心问卷收集与上传任务，禁用全部相关子任务。';
       risk = '反馈中心不再自动收集问卷。';
       recommendation = 'required'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"tasks":["\\Microsoft\\Windows\\Feedback\\Siuf\\DmClient","\\Microsoft\\Windows\\Feedback\\Siuf\\DmClientOnScenarioDownload","\\Microsoft\\Windows\\Feedback\\Siuf\\DmClientManager"]}'; enabled = $true; sortOrder = 60 },
    @{ code = 'task.wer-queue'; category = 'scheduled-task'; name = '禁用错误报告排队任务（QueueReporting）';
       principle = 'Windows 错误报告排队上传任务，崩溃数据后台上报。';
       risk = '崩溃数据不再自动上报，丢失诊断信息。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"tasks":["\\Microsoft\\Windows\\Windows Error Reporting\\QueueReporting"]}'; enabled = $true; sortOrder = 70 },
    @{ code = 'task.maps-toast'; category = 'scheduled-task'; name = '禁用地图通知任务（MapsToastTask）';
       principle = '地图应用通知任务；不用系统地图的机器可禁用。';
       risk = '系统地图通知失效。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"tasks":["\\Microsoft\\Windows\\Maps\\MapsToastTask"]}'; enabled = $true; sortOrder = 80 },
    @{ code = 'task.maps-update'; category = 'scheduled-task'; name = '禁用地图更新任务（MapsUpdateTask）';
       principle = '地图应用离线数据更新任务；不用系统地图的机器可禁用。';
       risk = '系统地图不再自动更新。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"tasks":["\\Microsoft\\Windows\\Maps\\MapsUpdateTask"]}'; enabled = $true; sortOrder = 90 },
    # ============ 三、可选功能类（5 条） ============
    @{ code = 'feature.ie11'; category = 'windows-feature'; name = '卸载 Internet Explorer 11';
       principle = '过时浏览器组件；现代系统已使用 Edge，IE11 仅作兼容遗留。';
       risk = '需要 IE 内核的极老网页/网银可能无法打开（可用 Edge IE 模式替代）。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"featureNames":["Internet-Explorer-Optional-amd64"]}'; enabled = $true; sortOrder = 10 },
    @{ code = 'feature.powershell2'; category = 'windows-feature'; name = '卸载 PowerShell 2.0';
       principle = '老旧 PowerShell 版本；系统自带 PowerShell 5.1+/7，无需保留。';
       risk = '依赖 PowerShell 2.0 的极老脚本可能无法运行。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"featureNames":["PowerShell-V2"]}'; enabled = $true; sortOrder = 20 },
    @{ code = 'feature.tiff-filter'; category = 'windows-feature'; name = '卸载 TIFF 图片筛选器';
       principle = 'TIFF 图片文字识别组件，普通人用不上。';
       risk = '无法用系统工具识别 TIFF 图片文字。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"featureNames":["WindowsTIFFIFilter"]}'; enabled = $true; sortOrder = 30 },
    @{ code = 'feature.xps-viewer'; category = 'windows-feature'; name = '卸载 XPS 文档查看器';
       principle = '极少使用的 XPS 文档格式查看组件。';
       risk = '无法打开 .xps 文档。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"featureNames":["XPS-Viewer"]}'; enabled = $true; sortOrder = 40 },
    @{ code = 'feature.fax'; category = 'windows-feature'; name = '卸载传真和扫描组件';
       principle = '传真相关组件；普通家用电脑无传真设备，纯属占用。';
       risk = '无法使用系统传真功能。';
       recommendation = 'optional'; riskLevel = 'safe'; isRecoverable = $true; targetJson = '{"featureNames":["FaxServicesClientPackage"]}'; enabled = $true; sortOrder = 50 }
)

foreach ($item in $new) {
    $kb.items += [pscustomobject]$item
}

$kb.kbVersion = 7

$kb | ConvertTo-Json -Depth 10 | Set-Content $path -Encoding UTF8
Write-Host "items=$($kb.items.Count) kbVersion=$($kb.kbVersion)"
