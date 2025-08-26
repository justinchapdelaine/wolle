export function el(tag, props = {}, ...children) {
  const e = document.createElement(tag)
  Object.assign(e, props)
  children.forEach((c) => e.append(typeof c === 'string' ? document.createTextNode(c) : c))
  return e
}
