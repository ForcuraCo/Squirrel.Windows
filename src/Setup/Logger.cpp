#include "stdafx.h"
#include "Logger.h"
#include <fstream>


std::string CLogger::getCurrentDateTime() {
	time_t now = time(0);
	struct tm  tstruct;
	char  buf[80];
	tstruct = *localtime(&now);

	strftime(buf, sizeof(buf), "[%d-%m-%Y %X]", &tstruct);

	return  std::string(buf);
};

void CLogger::Log(wchar_t *path, const char *level, const wchar_t *logMsg)
{
	std::wstring ws(logMsg);
	std::string strLogMsg(ws.begin(), ws.end());
	
	std::string now = getCurrentDateTime();
	std::ofstream ofs(path, std::ios_base::out | std::ios_base::app);
	ofs << now << '\t' << std::string(level) << '\t' << strLogMsg << '\n';

	ofs.close();
}