<template>
  <ElDropdown v-if="collapsed" trigger="click" placement="top-start" @command="handleChange">
    <div class="tenant-compact-trigger">
      <Icon icon="ep:office-building" :size="17" />
    </div>
    <template #dropdown>
      <ElDropdownMenu>
        <ElDropdownItem v-for="item in tenants" :key="item.id" :command="item.id">
          {{ item.name }}
        </ElDropdownItem>
      </ElDropdownMenu>
    </template>
  </ElDropdown>
  <div v-else>
    <el-select
      filterable
      placeholder="请选择租户"
      :class="sidebar ? 'sidebar-tenant-select' : 'tenant-top-select'"
      v-model="value"
      @change="handleChange"
      clearable
    >
      <el-option v-for="item in tenants" :key="item.id" :label="item.name" :value="item.id" />
    </el-select>
  </div>
</template>

<script lang="ts" setup>
import { ref, onMounted } from 'vue'
import * as TenantApi from '@/api/system/tenant'
import { getVisitTenantId, setVisitTenantId } from '@/utils/auth'
import { useMessage } from '@/hooks/web/useMessage'
import { useTagsView } from '@/hooks/web/useTagsView'

const message = useMessage() // 消息弹窗
const tagsView = useTagsView() // 标签页操作

const value = ref(getVisitTenantId()) // 当前选中的租户 ID
const tenants = ref<any[]>([]) // 租户列表

defineProps({
  collapsed: {
    type: Boolean,
    default: false
  },
  sidebar: {
    type: Boolean,
    default: false
  }
})

const handleChange = (id: number) => {
  // 设置访问租户 ID
  setVisitTenantId(id)
  // 关闭其他标签页，只保留当前页
  tagsView.closeOther()
  // 刷新当前页面
  tagsView.refreshPage()
  // 提示切换成功
  const tenant = tenants.value.find((item) => item.id === id)
  if (tenant) {
    message.success(`切换当前租户为: ${tenant.name}`)
  }
}

onMounted(async () => {
  tenants.value = await TenantApi.getTenantList()
})
</script>

<style scoped lang="scss">
.tenant-compact-trigger {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  color: var(--left-menu-text-color);
  cursor: pointer;
}

.sidebar-tenant-select {
  width: 100% !important;
}

.tenant-top-select {
  width: 180px !important;
}
</style>
