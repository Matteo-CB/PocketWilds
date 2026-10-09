// Pocket Wilds: the device's preferred languages for DeviceLanguages.cs (Services/Localization), iOS only.
// [NSLocale preferredLanguages] lists BCP 47 tags, most preferred first ("fr-FR", "zh-Hant-TW", "es-419"). Since iOS 13
// it starts with the per-app language chosen in the system settings, when Info.plist declares CFBundleLocalizations.
// The C# side declares: [DllImport("__Internal")] static extern string PWLanguages_Preferred();
// The returned buffer is malloc'd: the IL2CPP marshaller copies it into a managed string and frees it.
// Prototype for T39B (Docs/Design/Localization_Plan.md): not called by the game yet.

#import <Foundation/Foundation.h>
#include <stdlib.h>
#include <string.h>

extern "C" const char* PWLanguages_Preferred(void)
{
    @autoreleasepool
    {
        NSArray<NSString*>* languages = [NSLocale preferredLanguages];
        NSString* joined = languages.count > 0 ? [languages componentsJoinedByString:@","] : @"";
        const char* utf8 = [joined UTF8String];
        if (utf8 == NULL)
            utf8 = "";
        size_t length = strlen(utf8) + 1;
        char* copy = (char*)malloc(length);
        if (copy != NULL)
            memcpy(copy, utf8, length);
        return copy;
    }
}
