#include <iostream>
#include <windows.h>

int main()
{
	while (true)
	{
		bool is_ctrl = GetAsyncKeyState(VK_CONTROL);
		if (is_ctrl && GetAsyncKeyState('1'))
			system(R"(wt -F -w new-tab -p "CM" -d . cmd /k ".\..\AstralGates\bin\Debug\net10.0\AstralGates.exe")");
		if (is_ctrl && GetAsyncKeyState('2'))
			break;
		Sleep(100);
	}
}