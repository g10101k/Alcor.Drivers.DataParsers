Unicode true
ManifestDPIAware true
!define APPNAME "Indusoft.Alcor.DataServer.LIMSpackage"
!define APPPATH "Indusoft.Alcor.DataServer"
!define COMPANYNAME "InduSoft"
!define DESCRIPTION "Alcor for LIMS Integration"
!define LIMSTOOLSPATH "c:\"
!define BUILDSPATH "C:\LIMS_Resources\Builds"
RequestExecutionLevel admin ;Require admin rights on NT6+ (When UAC is turned on)
ShowInstDetails show
InstallDir "$PROGRAMFILES\${COMPANYNAME}"
Name "${COMPANYNAME} - ${APPNAME}"
outFile "Indusoft.Alcor.DataServer.LIMSpackage.exe"
 
!include LogicLib.nsh
 
# Just three pages - license agreement, install location, and installation
# page license
page directory
Page instfiles
 
!macro VerifyUserIsAdmin
UserInfo::GetAccountType
pop $0
${If} $0 != "admin" ;Require admin rights on NT4+
        messageBox mb_iconstop "Administrator rights required!"
        setErrorLevel 740 ;ERROR_ELEVATION_REQUIRED
        quit
${EndIf}
!macroend
 
function .onInit
	setShellVarContext all
	!insertmacro VerifyUserIsAdmin
functionEnd
 
section "install"
	setOutPath "c:\"
	File /nonfatal /r LIMS_Tools
	setOutPath ${BUILDSPATH}
	File /r /x I-ALR bin
	nsExec::ExecToLog /OEM '${BUILDSPATH}\bin\backup.bat'
	Delete "${BUILDSPATH}\bin\backup.bat"

	setOutPath $INSTDIR	
	nsExec::ExecToLog /OEM 'net stop InduSoft.Alcor.DataServer'
	File /r /x I-ALR Indusoft.Alcor.DataServer 
	nsExec::ExecToLog /OEM 'net start InduSoft.Alcor.DataServer'
	#DetailPrint 'xcopy /r /y "C:\LIMS_Resources\Builds\CfgBak\Config.xml" $INSTDIR'
	nsExec::ExecToLog /OEM 'xcopy /r /y "C:\LIMS_Resources\Builds\CfgBak\Config.xml" "$INSTDIR\${APPPATH}"'
	#nsExec::Exec [/OEM] [/TIMEOUT=x] path

sectionEnd