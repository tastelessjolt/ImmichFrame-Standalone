# Third-party software notices

ImmichFrame Standalone is licensed as a whole under the
[GNU General Public License version 3](LICENSE.txt). The Android application also distributes the
third-party components below under their respective compatible licenses. Those licenses apply to
the named components; they do not replace the GPLv3 terms for ImmichFrame Standalone itself.

This inventory describes the resolved Android release dependency graph for
`v1.0.15.0-standalone.9`. Build-only tooling and packages restored for non-Android runtimes are not
part of the APK.

## Fonts

| Component | Version | License and attribution |
| --- | --- | --- |
| Google Sans | 13.002 (2025) | SIL Open Font License 1.1. Copyright 2025 The Google Sans Project Authors. The complete license is in [`ImmichFrame/Assets/Fonts/LICENSE.txt`](ImmichFrame/Assets/Fonts/LICENSE.txt). |

## Managed and native dependencies

| Component or package family | Release version(s) | License | Copyright or source |
| --- | --- | --- | --- |
| Avalonia, Avalonia.Android, Avalonia.Skia, Avalonia.Themes.Fluent, Avalonia.Remote.Protocol | 11.2.0-beta1 | MIT | Copyright 2013–2024 The AvaloniaUI Project; [Avalonia](https://github.com/AvaloniaUI/Avalonia) |
| CommunityToolkit.Mvvm | 8.3.2 | MIT | .NET Foundation and contributors; [.NET Community Toolkit](https://github.com/CommunityToolkit/dotnet) |
| HarfBuzzSharp and HarfBuzzSharp.NativeAssets.Android | 7.3.0.2 | MIT, with bundled third-party components under their stated permissive licenses | Copyright 2015–2016 Xamarin, Inc.; 2017–2018 Microsoft Corporation; [SkiaSharp](https://github.com/mono/SkiaSharp) package notices |
| MicroCom.Runtime | 0.11.0 | MIT | Copyright 2021 Nikita Tsukanov; [MicroCom](https://github.com/kekekeks/MicroCom) |
| Microsoft.Extensions.Configuration, DependencyInjection, Logging, Options, and Primitives | 6.0.x | MIT | Microsoft Corporation and .NET Foundation; [dotnet/runtime](https://github.com/dotnet/runtime) |
| Newtonsoft.Json | 13.0.3 | MIT | Copyright 2007 James Newton-King; [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) |
| OpenWeatherMap.API | 2.1.2 | Apache-2.0 | Copyright 2024 Thomas Galliker; [OpenWeatherMap.API](https://github.com/thomasgalliker/OpenWeatherMapApi) |
| SkiaSharp and SkiaSharp.NativeAssets.Android | 2.88.8 | MIT, with bundled third-party components under their stated permissive licenses | Copyright 2015–2016 Xamarin, Inc.; 2017–2018 Microsoft Corporation; [SkiaSharp](https://github.com/mono/SkiaSharp) package notices |
| System.Runtime.CompilerServices.Unsafe | 6.0.0 | MIT | Microsoft Corporation and .NET Foundation; [dotnet/runtime](https://github.com/dotnet/runtime) |
| ThumbHash | 2.1.1 | MIT | [jzebedee/ThumbHash](https://github.com/jzebedee/ThumbHash) |
| UnitsNet | 5.29.0 | MIT | Copyright 2013 Andreas Gullberg Larsen; [UnitsNet](https://github.com/angularsen/UnitsNet) |
| Xamarin.AndroidX bindings (Activity, Annotation, AppCompat, Arch, Collection, Concurrent, Core, CursorAdapter, CustomView, DrawerLayout, Emoji2, Fragment, Interpolator, Lifecycle, Loader, ProfileInstaller, ResourceInspection, SavedState, Startup, Tracing, VectorDrawable, VersionedParcelable, ViewPager) | Package versions 1.0.0.21–1.12.0.2; Lifecycle 2.6.2.2; exact package list is in the release license bundle | MIT for Microsoft binding code; Apache-2.0 for upstream AndroidX artifacts | Microsoft Corporation and Android Open Source Project; [AndroidX](https://developer.android.com/jetpack/androidx) |
| Xamarin.Google.Guava.ListenableFuture | 1.0.0.16 | MIT for Microsoft binding code; Apache-2.0 for Guava | Microsoft Corporation and Google; [Guava](https://github.com/google/guava) |
| Xamarin.Jetbrains.Annotations | 24.0.1.5 | MIT for Microsoft binding code; Apache-2.0 for upstream annotations | Microsoft Corporation and JetBrains; [JetBrains annotations](https://github.com/JetBrains/java-annotations) |
| Xamarin.Kotlin.StdLib (Common, Jdk7, Jdk8) | 1.9.10.2 | MIT for Microsoft binding code; Apache-2.0 for Kotlin | Microsoft Corporation and JetBrains; [Kotlin](https://github.com/JetBrains/kotlin) |
| Xamarin.KotlinX.Coroutines (Android, Core.Jvm) | 1.7.3.2 | MIT for Microsoft binding code; Apache-2.0 for KotlinX Coroutines | Microsoft Corporation and JetBrains; [KotlinX Coroutines](https://github.com/Kotlin/kotlinx.coroutines) |

The GitHub release includes `Third-Party-License-Bundle.zip`. It contains the unmodified license and
third-party-notice files supplied by the restored SkiaSharp/HarfBuzzSharp, AndroidX, Kotlin,
KotlinX, Guava, JetBrains, and Newtonsoft.Json packages, together with this inventory and the
Google Sans OFL text. This preserves the packages' full native-library attribution and license
terms beside the distributed APK without making this overview thousands of lines long.

## MIT license text

The following permission text applies to the MIT-licensed components listed above. Their individual
copyright notices are retained in the table and in the release license bundle where supplied by the
package.

> Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
> associated documentation files (the “Software”), to deal in the Software without restriction,
> including without limitation the rights to use, copy, modify, merge, publish, distribute,
> sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
> furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all copies or
> substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
> NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
> NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
> DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
> OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

The complete Apache License 2.0 and additional native-library license texts are reproduced in the
package-supplied files inside the release license bundle.
