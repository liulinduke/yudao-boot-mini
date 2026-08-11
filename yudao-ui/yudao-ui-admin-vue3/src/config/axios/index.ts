import { service } from './service'

import { config } from './config'

const { default_headers } = config

const request = (option: any) => {
  const { headersType, headers, ...otherOption } = option
  return service({
    ...otherOption,
    headers: {
      'Content-Type': headersType || default_headers,
      ...headers
    }
  })
}

// The response interceptor normally unwraps the Axios response to the
// backend CommonResult object. Keep compatibility with callers and adapters
// that still return the raw Axios response.
const unwrapResponse = <T = any>(response: any): T => {
  if (response && Object.prototype.hasOwnProperty.call(response, 'data')) {
    return response.data as T
  }
  return response as T
}

export default {
  get: async <T = any>(option: any) => {
    const res = await request({ method: 'GET', ...option })
    return unwrapResponse<T>(res)
  },
  post: async <T = any>(option: any) => {
    const res = await request({ method: 'POST', ...option })
    return unwrapResponse<T>(res)
  },
  postOriginal: async (option: any) => {
    const res = await request({ method: 'POST', ...option })
    return res
  },
  delete: async <T = any>(option: any) => {
    const res = await request({ method: 'DELETE', ...option })
    return unwrapResponse<T>(res)
  },
  put: async <T = any>(option: any) => {
    const res = await request({ method: 'PUT', ...option })
    return unwrapResponse<T>(res)
  },
  download: async <T = any>(option: any) => {
    const res = await request({ method: 'GET', responseType: 'blob', ...option })
    return res as unknown as Promise<T>
  },
  upload: async <T = any>(option: any) => {
    option.headersType = 'multipart/form-data'
    const res = await request({ method: 'POST', ...option })
    return res as unknown as Promise<T>
  }
}
