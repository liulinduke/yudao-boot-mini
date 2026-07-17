<template>
  <div class="sidebar-tools" :class="{ 'is-collapsed': collapse }">
    <div class="sidebar-tool-list">
      <div class="sidebar-menu-item sidebar-message">
        <Message color="var(--left-menu-text-color)" placement="top" />
      </div>
      <div class="sidebar-menu-item sidebar-user">
        <UserInfo placement="top-end" :show-name="false" :hoverable="false" compact />
      </div>
    </div>

    <div v-if="includeCollapse" class="sidebar-collapse">
      <Collapse color="var(--left-menu-text-color)" />
    </div>
  </div>
</template>

<script lang="ts" setup>
import { Collapse } from '@/layout/components/Collapse'
import { Message } from '@/layout/components/Message'
import { UserInfo } from '@/layout/components/UserInfo'
import { useAppStore } from '@/store/modules/app'

defineOptions({ name: 'SidebarTools' })

const props = withDefaults(
  defineProps<{
    includeCollapse?: boolean
  }>(),
  {
    includeCollapse: true
  }
)

const appStore = useAppStore()
const collapse = computed(() => appStore.getCollapse)
const includeCollapse = computed(() => props.includeCollapse)
</script>

<style lang="scss" scoped>
.sidebar-tools {
  flex: none;
  width: 100%;
  min-width: 0;
  max-width: 100%;
  box-sizing: border-box;
  overflow: hidden;
  padding: 6px 0 8px;
  background: var(--left-menu-bg-color);
  color: var(--left-menu-text-color);
}

.sidebar-collapse {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 36px;
  border-top: 1px solid rgb(255 255 255 / 10%);
  cursor: pointer;
}

.sidebar-tool-list {
  display: flex;
  flex-direction: column;
  align-items: stretch;
  gap: 2px;
  min-width: 0;
  max-width: 100%;
  margin-top: 4px;
}

.sidebar-menu-item {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  min-height: 42px;
  box-sizing: border-box;
  overflow: hidden;
  padding: 0;
}

.is-collapsed {
  padding-right: 6px;
  padding-left: 6px;

  .sidebar-tool-list {
    justify-content: center;
    align-items: center;
  }

  .sidebar-menu-item {
    width: 100%;
    min-height: 34px;
  }
}

:deep(.sidebar-message .message),
:deep(.sidebar-user .user-info),
:deep(.sidebar-user .el-dropdown) {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
}

:deep(.sidebar-user .custom-hover) {
  width: 100%;
  height: 100%;
  padding: 0;
  background: transparent;
}

:deep(.sidebar-user .custom-hover:hover) {
  background: transparent !important;
}

:deep(.sidebar-user .el-avatar) {
  width: 28px;
  height: 28px;
}
</style>
