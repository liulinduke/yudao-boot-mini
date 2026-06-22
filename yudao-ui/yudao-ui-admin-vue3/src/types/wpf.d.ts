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
                taskId: string,
                accountId: string,
                cookie: string | null,
                url: string,
                expectedCount: number,
                taskType?: number,
                config?: string | null,
                isOperation?: boolean
              ) => void
              StopBrowser?: (accountId: string) => void
              StartAccountLoginBatch: (
                accountsJson: string
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
