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
              OpenMessageManagerWindow?: () => void
              StopBrowser?: (accountId: string) => void
              GetAvailableBrowserSlots?: () => number
              OpenMessageBrowser?: (accountId: string, cookie: string, deviceId: string, url: string) => void
              OpenMessageConversation?: (accountId: string, targetUserId: string, url: string) => void
              SetMessageBrowserBounds?: (left: number, top: number, width: number, height: number) => void
              StartMessageMonitor?: (monitorId: string, accountId: string, cookie: string, deviceId: string, url: string, mode: string) => void
              CloseMessageBrowser?: () => void
              CloseMessageBrowserAccount?: (accountId: string) => void
              HideMessageBrowser?: () => void
              StartDmTask?: (
                taskId: string,
                detailId: string,
                accountId: string,
                cookie: string,
                targetUserId: string,
                scriptContent: string
              ) => void
              StartGroupPublishTask?: (
                taskId: string,
                accountId: string,
                cookie: string,
                actionConfigJson: string,
                detailId?: string
              ) => void
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
