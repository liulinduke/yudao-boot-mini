/**
 * WPF CefSharp 桥接工具
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
                taskType: number,
                config?: string,
                isOperation?: boolean
              ) => void
              StopBrowser?: (accountId: string) => void
              StartAccountLoginBatch: (accountsJson: string) => void
            }
          }
        }
        addEventListener: (event: string, callback: (data: any) => void) => void
      }
    }
  }
}

export interface FbAccountLoginBridgePayload {
  id: number
  accountId: string
  password?: string
  tfa?: string
  cookie?: string | null
}

export interface FbAccountLoginBridgeResult {
  accountDbId: number
  accountId: string
  status: 'pending' | 'running' | 'success' | 'failed' | 'skipped'
  loginMode?: 'cookie' | 'credential'
  errorReason?: string
  cookieSaved?: boolean
  windowClosed?: boolean
}

export function startBrowserCollect(
  taskId: string,
  accountId: string,
  cookie: string | null,
  url: string,
  expectedCount: number,
  taskType: number = 1,
  config?: string,
  isOperation: boolean = false
): void {
  try {
    if (window.chrome?.webview?.hostObjects?.sync?.wpfBridge) {
      const bridge = window.chrome.webview.hostObjects.sync.wpfBridge
      if (config) {
        try {
          bridge.StartBrowser(taskId, accountId, cookie, url, expectedCount, taskType, config, isOperation)
        } catch (e) {
          bridge.StartBrowser(taskId, accountId, cookie, url, expectedCount, taskType, config)
        }
      } else {
        bridge.StartBrowser(taskId, accountId, cookie, url, expectedCount, taskType, null, isOperation)
      }
    } else {
      console.warn('WPF 桥接未就绪，请在 WPF 环境中运行')
    }
  } catch (error) {
    console.error('启动浏览器失败', error)
    throw error
  }
}

export function onCollectionComplete(callback: (data: any) => void): void {
  try {
    window.addEventListener('fb:collection:complete', (event: any) => {
      const detail = event.detail || {}
      const results = detail.data ?? detail.results ?? []

      callback({
        type: 'CollectionComplete',
        detailId: detail.detailId,
        accountId: detail.accountId,
        taskType: detail.taskType,
        results,
        count: Array.isArray(results) ? results.length : 0,
        timestamp: detail.timestamp
      })
    })
  } catch (error) {
    console.error('注册采集完成事件失败', error)
  }
}

export function closeBrowser(accountId: string): void {
  try {
    const bridge = window.chrome?.webview?.hostObjects?.sync?.wpfBridge
    if (bridge?.StopBrowser) {
      bridge.StopBrowser(accountId)
    } else {
      console.warn('WPF 桥接未就绪或不支持关闭浏览器')
    }
  } catch (error) {
    console.error('关闭浏览器失败', error)
    throw error
  }
}

export function startAccountLoginBatch(accounts: FbAccountLoginBridgePayload[]): void {
  try {
    if (window.chrome?.webview?.hostObjects?.sync?.wpfBridge) {
      window.chrome.webview.hostObjects.sync.wpfBridge.StartAccountLoginBatch(JSON.stringify(accounts))
    } else {
      console.warn('WPF 桥接未就绪，请在 WPF 环境中运行')
    }
  } catch (error) {
    console.error('启动批量登录失败', error)
    throw error
  }
}

export function onAccountLoginProgress(callback: (data: FbAccountLoginBridgeResult) => void): void {
  window.addEventListener('fb:account-login:progress', (event: any) => callback(event.detail))
}

export function onAccountLoginComplete(
  callback: (data: { summary: { total: number; success: number; failed: number; skipped: number }; results: FbAccountLoginBridgeResult[] }) => void
): void {
  window.addEventListener('fb:account-login:complete', (event: any) => callback(event.detail))
}
