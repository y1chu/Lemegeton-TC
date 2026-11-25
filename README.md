# Lemegeton TC

[Lemegeton](https://github.com/paissaheavyindustries/Lemegeton) 的台服修改版，從原版 1.0.7.6 改來，有繁體中文介面。

目前對應台服 `2026.11.05.0000.0000`，需要台服 Dalamud API 13。

Repo Link:
```
https://raw.githubusercontent.com/k1miyama/DalamudPlugins/refs/heads/main/pluginmaster.json
```
## 編譯

先裝好 .NET 9 SDK 和台服 Dalamud，再到這個專案的資料夾，用 PowerShell 執行：

```powershell
./build-tc.ps1
```

預設會編譯 Mini 版。需要 Full 版的額外功能時，改用：

```powershell
./build-tc.ps1 -Configuration 'Release (Full)'
```

編好的壓縮檔會放在 `artifacts` 資料夾，檔名是 `Lemegeton-TC-mini.zip` 或 `Lemegeton-TC-full.zip`。

編譯時會到 `%APPDATA%/FFXIVSimpleLauncher/Dalamud/Injector` 找 Dalamud。裝在別的地方時，用 `-DalamudLibPath` 指定路徑；也可以用 `-DotnetPath` 指定 `dotnet.exe` 的位置。

## 裝進遊戲

到 Dalamud 設定裡加入開發插件路徑，填入 `artifacts/tc-mini/Lemegeton.dll` 的完整路徑，再到插件列表啟用。Full 版則用 `artifacts/tc-full/Lemegeton.dll`。

Mini 和 Full 選一個用，不要同時開。更新檔案後，要把插件關掉再開一次才會生效。

## 標記怎麼用

預設只會顯示自己看得到的「本機標記」，不會更動遊戲裡全隊都看得到的標記。

可以先在聊天欄輸入這兩行，看看自己頭上有沒有出現標記：

```text
/lemmy drawing on
/lemmy mark attack1 <me>
```

輸入 `/lemmy mark off <me>` 就能移除。測試前先收起「本機標記設定」，以免只看到預覽的標記。

如果想讓全隊都看得到標記：

1. 到「設定 → 危險區域」，看完提示後勾選確認。
2. 輸入 `ALLOW SHARED MARKERS`，再按「停用強制本機標記保護」。
3. 回到標記設定，取消勾選「使用本機標記」。

想切回來時，按「重新啟用強制本機標記保護」就好。原本遊戲裡的標記不會被清掉。

[原版說明](https://github.com/paissaheavyindustries/Lemegeton/wiki) · [MIT 授權](LICENSE)
