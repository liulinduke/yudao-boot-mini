<template>
  <div
    class="operation-card"
    :class="{ 'is-disabled': disabled, 'is-active': active }"
    @click="handleClick"
  >
    <div class="card-icon">
      <Icon :icon="icon" :size="24" />
    </div>
    <div class="card-content">
      <h4 class="card-title">{{ title }}</h4>
    </div>
    <div v-if="disabled" class="card-badge">
      <el-tag size="small" type="info">开发中</el-tag>
    </div>
  </div>
</template>

<script setup lang="ts">
defineOptions({ name: 'OperationCard' })

interface Props {
  title: string
  icon?: string
  active?: boolean
  disabled?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  icon: 'ep:document',
  active: false,
  disabled: false
})

const emit = defineEmits<{
  click: []
}>()

const handleClick = () => {
  if (!props.disabled) {
    emit('click')
  }
}
</script>

<style scoped lang="scss">
.operation-card {
  position: relative;
  display: flex;
  align-items: center;
  padding: 12px;
  margin-bottom: 12px;
  border: 2px solid var(--el-color-primary-light-7);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.3s;
  background-color: var(--el-color-primary-light-9);
  color: var(--el-text-color-primary);

  &:hover:not(.is-disabled) {
    border-color: var(--el-color-primary);
    background-color: var(--el-color-primary-light-8);
    box-shadow: 0 4px 12px 0 rgba(0, 0, 0, 0.15);
    transform: translateY(-2px);
  }

  &.is-active {
    border-color: var(--el-color-primary);
    background-color: var(--el-color-primary-light-8);
    box-shadow: 0 2px 8px 0 rgba(0, 0, 0, 0.1);
  }

  &.is-disabled {
    opacity: 0.5;
    cursor: not-allowed;
    background-color: var(--el-fill-color-light);
    border-color: var(--el-border-color-lighter);
  }

  .card-icon {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 48px;
    height: 48px;
    border-radius: 8px;
    background-color: var(--el-color-primary);
    color: #ffffff;
    margin-right: 16px;
    flex-shrink: 0;
  }

  .card-content {
    flex: 1;

    .card-title {
      margin: 0;
      font-size: 14px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }
  }

  .card-badge {
    position: absolute;
    top: 5px;
    right: 5px;
  }
}
</style>
