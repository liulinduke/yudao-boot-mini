import request from '@/config/axios'

export type FbResourceType = 'LEAD' | 'GROUP' | 'POST'
export interface FbResourceGroup {
  id: number
  name: string
  resourceType: FbResourceType
  isDefault?: boolean
}

export const FbResourceGroupApi = {
  getList: (resourceType: FbResourceType) =>
    request.get<FbResourceGroup[]>({ url: '/facebook/resource-group/list', params: { resourceType } }),
  create: (data: { name: string; resourceType: FbResourceType }) =>
    request.post({ url: '/facebook/resource-group/create', data }),
  update: (data: { id: number; name: string; resourceType: FbResourceType }) =>
    request.put({ url: '/facebook/resource-group/update', data }),
  delete: (id: number) => request.delete({ url: `/facebook/resource-group/delete?id=${id}` })
}
