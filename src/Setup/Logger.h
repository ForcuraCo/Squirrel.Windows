#pragma once
#include <string>

class CLogger
{
public:
	static void Log(wchar_t *path, const char *level, const wchar_t *logMsg);

private:
	static std::string getCurrentDateTime();
};
