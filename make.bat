
del /s /q DataServer\*.pdb
del /s /q DataServer\Indusoft.Alcor.DataServer.vshost.*
del /s /q Alcor.Lims.Pack.exe 
del /s /q Indusoft.Alcor.DataServer.limspath.exe 
rem del /s /q DataServer.7z
rem del /s /q ReinstallAlcor.zip
rem bin\7z.exe a -r DataServer.7z %~dp0DataServer

rem bin\7z.exe a -r -tzip ReinstallAlcor.zip DataServer.7z

rem bin\7z.exe a -tzip ReinstallAlcor.zip %~dp0bin
rem bin\7z.exe a -tzip ReinstallAlcor.zip %~dp0reinstall*.bat
xcopy /r /y /e  DataServer Indusoft.Alcor.DataServer\
del /s /q Indusoft.Alcor.DataServer\Config.xml 
@d:\_portable\PortableApps\NSISPortable\NSISPortable.exe make.nsi 

@ rmdir /s /q Indusoft.Alcor.DataServer
