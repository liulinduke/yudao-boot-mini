import { FbAccountApi, type FbProxyConfig } from '@/api/facebook/account'

export async function getFbAccountProxyJson(accountId?: string | number): Promise<string | undefined> {
  const key = String(accountId || '').trim()
  if (!key) return undefined
  const proxy = await FbAccountApi.getRuntimeProxy(key)
  return proxy ? JSON.stringify(proxy as FbProxyConfig) : undefined
}
