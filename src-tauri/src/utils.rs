pub fn format_prompt(action: &str, input: &str) -> String {
  format!("Action: {}\n\n{}", action.trim(), input.trim())
}

#[cfg(test)]
mod tests {
  use super::*;

  #[test]
  fn formats_prompt_correctly() {
    let p = format_prompt(" summarize ", "  Hello World  ");
    assert_eq!(p, "Action: summarize\n\nHello World");
  }
}
