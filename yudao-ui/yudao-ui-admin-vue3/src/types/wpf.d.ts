/**
 * WPF CefSharp 桥接类型定义
 */

declare global {
  interface Window {
    chrome?: {
      webview?: {
        hostObjects?: {
          sync?: {
            wpfBridge?: {
              StartBrowser: (
                accountId: number,
                cookie: string | null,
                url: string,
                expectedCount: number
              ) => void
            }
          }
        }
        addEventListener: (event: string, callback: (data: any) => void) => void
      }
    }
  }
}

export {}
